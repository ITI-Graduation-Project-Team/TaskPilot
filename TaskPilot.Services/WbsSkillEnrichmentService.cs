using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<WbsSkillEnrichmentService> _logger;

        public WbsSkillEnrichmentService(
            IRepository<Project> projectRepository,
            IRepository<TaskItem> taskRepository,
            IRepository<Skill> skillRepository,
            IRepository<TaskRequiredSkill> taskRequiredSkillRepository,
            RequiredSkillsEnrichmentAgent agent,
            IUnitOfWork unitOfWork,
            ILogger<WbsSkillEnrichmentService> logger)
        {
            _projectRepository = projectRepository;
            _taskRepository = taskRepository;
            _skillRepository = skillRepository;
            _taskRequiredSkillRepository = taskRequiredSkillRepository;
            _agent = agent;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<SkillEnrichmentResult>> EnrichProjectTasksAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            // 1. Verify project exists
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return Result.Failure<SkillEnrichmentResult>(WbsErrors.ProjectNotFound);

            // Load all project tasks so retries report cumulative coverage instead of
            // replacing the counters with the size of the latest retry batch.
            var projectTasks = (await _taskRepository.FindAsync(
                t => t.UserStory != null && t.UserStory.ProjectId == projectId,
                t => t.RequiredSkills))
                .ToList();

            var eligibleTasks = projectTasks
                .Where(t => t.Type != TaskType.NonTechnical)
                .ToList();
            var alreadyEnriched = eligibleTasks.Count(t => t.RequiredSkills.Any());
            var tasksToEnrich = eligibleTasks
                .Where(t => !t.RequiredSkills.Any())
                .ToList();

            if (!tasksToEnrich.Any())
                return Result.Success(new SkillEnrichmentResult
                {
                    TasksProcessed = eligibleTasks.Count,
                    TasksEnriched = alreadyEnriched,
                    TasksSkipped = 0,
                    SkillsCreated = 0
                });

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

            // Keep AI pressure bounded. Per-task retries add exponential backoff and jitter.
            const int maxConcurrentAiCalls = 5;
            using var semaphore = new SemaphoreSlim(maxConcurrentAiCalls, maxConcurrentAiCalls);

            int tasksEnriched = 0;
            var warnings = new List<string>();

            // Fix 2: Replace sequential foreach with Task.WhenAll over all technical tasks
            var parallelTasks = tasksToEnrich.Select(task => ProcessSingleTaskAsync(
                task, availableSkillNames, skillDict, newSkillsToInsert,
                newRequiredSkillsToSave, processedTaskSkillPairs,
                semaphore, cancellationToken));

            var perTaskResults = await Task.WhenAll(parallelTasks);

            // Aggregate counts
            foreach (var r in perTaskResults)
            {
                tasksEnriched += r.enriched ? 1 : 0;
                if (!string.IsNullOrWhiteSpace(r.warning))
                    warnings.Add(r.warning);
            }

            var totalEnriched = alreadyEnriched + tasksEnriched;
            var resultStats = new SkillEnrichmentResult
            {
                TasksProcessed = eligibleTasks.Count,
                TasksEnriched = totalEnriched,
                TasksSkipped = eligibleTasks.Count - totalEnriched,
                Warnings = warnings
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
        /// thread-safe in-memory collections. Returns an enriched flag and an optional warning.
        /// </summary>
        private async Task<(bool enriched, string? warning)> ProcessSingleTaskAsync(
            TaskItem task,
            List<string> availableSkillNames,
            ConcurrentDictionary<string, Skill> skillDict,
            ConcurrentDictionary<string, Skill> newSkillsToInsert,
            ConcurrentBag<TaskRequiredSkill> newRequiredSkillsToSave,
            ConcurrentDictionary<string, bool> processedTaskSkillPairs,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken)
        {
            if (task.Type == TaskType.NonTechnical)
                return (false, null);

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
                    _logger.LogWarning(
                        "Required-skill enrichment skipped technical task {TaskId}. Reason={ReasonCode}",
                        task.Id,
                        agentResult.Error.Code);
                    return (false, $"Task {task.Id}: {agentResult.Error.Code}");
                }

                var generatedSkills = agentResult.Value;
                if (generatedSkills == null || !generatedSkills.Any())
                    return (false, $"Task {task.Id}: REQUIRED_SKILLS_EMPTY");

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

                if (enriched)
                    return (true, null);

                _logger.LogWarning(
                    "Required-skill enrichment produced no persistable skills for technical task {TaskId}.",
                    task.Id);
                return (false, $"Task {task.Id}: INVALID_GENERATED_SKILL");
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
