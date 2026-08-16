using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class WbsSkillEnrichmentService : IWbsSkillEnrichmentService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IRepository<Skill> _skillRepository;
        private readonly IRepository<TaskRequiredSkill> _taskRequiredSkillRepository;
        private readonly RequiredSkillsEnrichmentAgent _agent;
        private readonly IUnitOfWork _unitOfWork;

        public WbsSkillEnrichmentService(
            IRepository<Project> projectRepository,
            IRepository<TaskItem> taskRepository,
            IRepository<Skill> skillRepository,
            IRepository<TaskRequiredSkill> taskRequiredSkillRepository,
            RequiredSkillsEnrichmentAgent agent,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _taskRepository = taskRepository;
            _skillRepository = skillRepository;
            _taskRequiredSkillRepository = taskRequiredSkillRepository;
            _agent = agent;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<SkillEnrichmentResult>> EnrichProjectTasksAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            // 1. Verify Project exists
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<SkillEnrichmentResult>(WbsErrors.ProjectNotFound);
            }

            // 2. Load TaskItems without RequiredSkills
            var tasksToEnrichEnumerable = await _taskRepository.FindAsync(t => t.UserStory != null && t.UserStory.ProjectId == projectId && !t.RequiredSkills.Any());
            var tasksToEnrich = tasksToEnrichEnumerable.ToList();

            if (!tasksToEnrich.Any())
            {
                return Result.Success(new SkillEnrichmentResult
                {
                    TasksProcessed = 0,
                    TasksEnriched = 0,
                    TasksSkipped = 0,
                    SkillsCreated = 0
                });
            }

            // 3. Load Available Skills
            // We only need names for the prompt. To avoid loading all skills, we'll project just the names.
            // But since IRepository doesn't expose projection directly, we'll fetch all or just use a basic list if it gets too large.
            // For now, let's keep available skills for the prompt if they are reasonably sized.
            var allSkills = await _skillRepository.GetAllAsync();
            var availableSkillNames = allSkills.Select(s => s.Name).Distinct().ToList();

            var resultStats = new SkillEnrichmentResult
            {
                TasksProcessed = tasksToEnrich.Count
            };

            var newRequiredSkillsToSave = new List<TaskRequiredSkill>();

            // Duplicate prevention across tasks in current run
            var skillDict = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
            var processedTaskSkillPairs = new HashSet<string>();

            // 4. Iterate over tasks
            foreach (var task in tasksToEnrich)
            {
                if (task.Type == TaskType.NonTechnical)
                {
                    resultStats.TasksSkipped++;
                    continue;
                }

                // Call Agent
                var agentResult = await _agent.EnrichAsync(
                    task.TitleEn,
                    task.DescriptionEn ?? string.Empty,
                    task.Type.ToString(),
                    availableSkillNames,
                    projectId,
                    cancellationToken);

                if (agentResult.IsFailure)
                {
                    return Result.Failure<SkillEnrichmentResult>(agentResult.Error);
                }

                var generatedSkills = agentResult.Value;
                if (generatedSkills == null)
                {
                    return Result.Failure<SkillEnrichmentResult>(WbsErrors.RequiredSkillsEmpty);
                }

                if (!generatedSkills.Any())
                {
                    resultStats.TasksSkipped++;
                    continue;
                }

                bool enriched = false;

                foreach (var generatedSkill in generatedSkills)
                {
                    if (string.IsNullOrWhiteSpace(generatedSkill.SkillName))
                    {
                        return Result.Failure<SkillEnrichmentResult>(WbsErrors.InvalidGeneratedSkill);
                    }

                    if (!Enum.TryParse<SkillLevel>(generatedSkill.RequiredLevel, true, out var requiredLevel))
                    {
                        return Result.Failure<SkillEnrichmentResult>(WbsErrors.InvalidRequiredLevel);
                    }

                    var normalizedName = SkillNormalizer.Normalize(generatedSkill.SkillName);
                    if (string.IsNullOrWhiteSpace(normalizedName))
                    {
                        return Result.Failure<SkillEnrichmentResult>(WbsErrors.SkillNormalizationFailed);
                    }

                    // 5. Missing Skill Resolution
                    if (!skillDict.TryGetValue(normalizedName, out var skillEntity))
                    {
                        skillEntity = await _skillRepository.FindSingleAsync(s => s.NormalizedName == normalizedName);
                        
                        if (skillEntity == null)
                        {
                            skillEntity = new Skill
                            {
                                Name = generatedSkill.SkillName,
                                NormalizedName = normalizedName
                            };
                            
                            try
                            {
                                await _skillRepository.AddAsync(skillEntity);
                                await _unitOfWork.SaveChangesAsync(cancellationToken);
                                resultStats.SkillsCreated++;
                            }
                            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                            {
                                _skillRepository.Delete(skillEntity); // Detach failed entity
                                skillEntity = await _skillRepository.FindSingleAsync(s => s.NormalizedName == normalizedName);
                                if (skillEntity == null) throw; // Should exist if we hit duplicate key
                            }
                        }
                        
                        skillDict[normalizedName] = skillEntity;
                    }

                    // 6. Duplicate Prevention
                    var pairKey = $"{task.Id}_{normalizedName}";
                    if (processedTaskSkillPairs.Add(pairKey))
                    {
                        newRequiredSkillsToSave.Add(new TaskRequiredSkill
                        {
                            TaskId = task.Id,
                            Task = task,
                            Skill = skillEntity,
                            RequiredLevel = requiredLevel
                        });
                        enriched = true;
                    }
                }

                if (enriched)
                {
                    resultStats.TasksEnriched++;
                }
                else
                {
                    resultStats.TasksSkipped++;
                }
            }

            // 7. Add entities (Service does not call SaveChangesAsync)
            try
            {
                if (newRequiredSkillsToSave.Any())
                {
                    await _taskRequiredSkillRepository.AddRangeAsync(newRequiredSkillsToSave);
                }

                return Result.Success(resultStats);
            }
            catch (Exception)
            {
                return Result.Failure<SkillEnrichmentResult>(WbsErrors.RequiredSkillsPersistenceFailed);
            }
        }
    }
}
