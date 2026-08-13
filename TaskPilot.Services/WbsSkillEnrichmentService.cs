using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            // 1. Verify project exists
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return Result.Failure<SkillEnrichmentResult>(WbsErrors.ProjectNotFound);

            // 2. Load tasks that still need enrichment
            var tasksToEnrich = (await _taskRepository.FindAsync(
                t => t.UserStory != null && t.UserStory.ProjectId == projectId && !t.RequiredSkills.Any()))
                .ToList();

            if (!tasksToEnrich.Any())
                return Result.Success(new SkillEnrichmentResult { TasksProcessed = 0, TasksEnriched = 0, TasksSkipped = 0, SkillsCreated = 0 });

            // 3. Fix 4: bulk-load ALL existing skills once before any LLM call — no DB reads inside the loop
            var allSkills = await _skillRepository.GetAllAsync();
            var availableSkillNames = allSkills.Select(s => s.Name).Distinct().ToList();

            // Fix 2: ConcurrentDictionary so parallel tasks share the skill lookup safely
            var skillDict = new ConcurrentDictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allSkills)
                if (!string.IsNullOrWhiteSpace(s.NormalizedName))
                    skillDict.TryAdd(s.NormalizedName, s);

            // Fix 4: collections that survive parallel writes safely
            var newSkillsToInsert = new ConcurrentDictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
            var newRequiredSkillsToSave = new ConcurrentBag<TaskRequiredSkill>();
            var processedTaskSkillPairs = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            // Fix 2: failure tracking — collect instead of early-return inside parallel tasks
            var firstFailure = new ConcurrentDictionary<int, Error>();

            // Fix 2: SemaphoreSlim(10) — max 10 concurrent gpt-4o-mini calls; respects API rate limits
            using var semaphore = new SemaphoreSlim(10, 10);

            int tasksEnriched = 0;
            int tasksSkipped = 0;

            // Fix 2: Replace sequential foreach with Task.WhenAll over all technical tasks
            var parallelTasks = tasksToEnrich.Select((task, idx) => ProcessSingleTaskAsync(
                task, idx, availableSkillNames, skillDict, newSkillsToInsert,
                newRequiredSkillsToSave, processedTaskSkillPairs, firstFailure,
                semaphore, cancellationToken));

            var perTaskResults = await Task.WhenAll(parallelTasks);

            // Surface the first failure if any task failed
            if (firstFailure.Any())
                return Result.Failure<SkillEnrichmentResult>(firstFailure.Values.First());

            // Aggregate counts
            foreach (var r in perTaskResults)
            {
                tasksEnriched += r.enriched ? 1 : 0;
                tasksSkipped += r.skipped ? 1 : 0;
            }

            var resultStats = new SkillEnrichmentResult
            {
                TasksProcessed = tasksToEnrich.Count,
                TasksEnriched = tasksEnriched,
                TasksSkipped = tasksSkipped
            };

            // Fix 4: single bulk persist after Task.WhenAll — no SaveChangesAsync inside the loop
            try
            {
                if (!newSkillsToInsert.IsEmpty)
                    await _skillRepository.AddRangeAsync(newSkillsToInsert.Values);

                if (!newRequiredSkillsToSave.IsEmpty)
                    await _taskRequiredSkillRepository.AddRangeAsync(newRequiredSkillsToSave);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                resultStats.SkillsCreated = newSkillsToInsert.Count;
                return Result.Success(resultStats);
            }
            catch (Exception)
            {
                return Result.Failure<SkillEnrichmentResult>(WbsErrors.RequiredSkillsPersistenceFailed);
            }
        }

        /// <summary>
        /// Fix 2: processes one task in parallel. No EF Core calls — only reads/writes to
        /// thread-safe in-memory collections. Returns (enriched, skipped) flags.
        /// </summary>
        private async Task<(bool enriched, bool skipped)> ProcessSingleTaskAsync(
            TaskItem task,
            int taskIndex,
            List<string> availableSkillNames,
            ConcurrentDictionary<string, Skill> skillDict,
            ConcurrentDictionary<string, Skill> newSkillsToInsert,
            ConcurrentBag<TaskRequiredSkill> newRequiredSkillsToSave,
            ConcurrentDictionary<string, bool> processedTaskSkillPairs,
            ConcurrentDictionary<int, Error> firstFailure,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken)
        {
            if (task.Type == TaskType.NonTechnical)
                return (false, true);

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Fix 3: _agent uses cached Kernel + KernelFunction — no rebuild per call
                var agentResult = await _agent.EnrichAsync(
                    task.TitleEn,
                    task.DescriptionEn ?? string.Empty,
                    task.Type.ToString(),
                    availableSkillNames,
                    cancellationToken);

                if (agentResult.IsFailure)
                {
                    firstFailure.TryAdd(taskIndex, agentResult.Error);
                    return (false, false);
                }

                var generatedSkills = agentResult.Value;
                if (generatedSkills == null || !generatedSkills.Any())
                    return (false, true);

                bool enriched = false;

                foreach (var generatedSkill in generatedSkills)
                {
                    if (string.IsNullOrWhiteSpace(generatedSkill.SkillName))
                        continue;

                    if (!Enum.TryParse<SkillLevel>(generatedSkill.RequiredLevel, true, out var requiredLevel))
                        continue;

                    var normalizedName = SkillNormalizer.Normalize(generatedSkill.SkillName);
                    if (string.IsNullOrWhiteSpace(normalizedName))
                        continue;

                    // Resolve skill: existing in DB or created this run — no DB calls here (Fix 4)
                    var skillEntity = skillDict.GetOrAdd(normalizedName, key =>
                        newSkillsToInsert.GetOrAdd(key,
                            _ => new Skill { Name = generatedSkill.SkillName, NormalizedName = key }));

                    var pairKey = $"{task.Id}_{normalizedName}";
                    if (processedTaskSkillPairs.TryAdd(pairKey, true))
                    {
                        newRequiredSkillsToSave.Add(new TaskRequiredSkill
                        {
                            TaskId = task.Id,
                            Skill = skillEntity,
                            RequiredLevel = requiredLevel
                        });
                        enriched = true;
                    }
                }

                return enriched ? (true, false) : (false, true);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
