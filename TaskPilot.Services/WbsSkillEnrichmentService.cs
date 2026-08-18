using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services;

public class WbsSkillEnrichmentService : IWbsSkillEnrichmentService
{
    internal const int BatchSize = 25;
    internal const int MaxConcurrentBatches = 5;
    internal const int MaxTaskAttempts = 3;

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
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return Result.Failure<SkillEnrichmentResult>(WbsErrors.ProjectNotFound);

        var projectTasks = (await _taskRepository.FindAsync(
            task => task.UserStory != null && task.UserStory.ProjectId == projectId,
            task => task.RequiredSkills)).ToList();

        var eligibleTasks = projectTasks.Where(task => task.Type != TaskType.NonTechnical).ToList();
        var alreadyEnriched = eligibleTasks.Count(task => task.RequiredSkills.Any());
        var pendingTasks = eligibleTasks.Where(task => !task.RequiredSkills.Any()).ToList();

        if (pendingTasks.Count == 0)
        {
            return Result.Success(new SkillEnrichmentResult
            {
                TasksProcessed = eligibleTasks.Count,
                TasksEnriched = alreadyEnriched,
                TasksSkipped = 0,
                SkillsCreated = 0
            });
        }

        var allSkills = (await _skillRepository.GetAllAsync()).ToList();
        var availableSkillNames = allSkills
            .Select(skill => skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var generatedByTask = new Dictionary<Guid, List<GeneratedRequiredSkill>>();
        var failureReasonByTask = new Dictionary<Guid, string>();

        for (var attempt = 1; attempt <= MaxTaskAttempts && pendingTasks.Count > 0; attempt++)
        {
            var batchResults = await RunBatchRoundAsync(
                pendingTasks,
                availableSkillNames,
                cancellationToken);

            foreach (var batchResult in batchResults)
            {
                if (batchResult.Result.IsFailure)
                {
                    foreach (var task in batchResult.Tasks)
                        failureReasonByTask[task.Id] = batchResult.Result.Error.Code;
                    continue;
                }

                var allowedTaskIds = batchResult.Tasks.Select(task => task.Id).ToHashSet();
                foreach (var generated in batchResult.Result.Value.Where(item => allowedTaskIds.Contains(item.TaskId)))
                {
                    if (generated.Skills.Count == 0)
                        continue;

                    generatedByTask[generated.TaskId] = generated.Skills;
                    failureReasonByTask.Remove(generated.TaskId);
                }

                foreach (var task in batchResult.Tasks.Where(task => !generatedByTask.ContainsKey(task.Id)))
                    failureReasonByTask[task.Id] = "REQUIRED_SKILLS_MISSING_FROM_BATCH";
            }

            pendingTasks = pendingTasks
                .Where(task => !generatedByTask.ContainsKey(task.Id))
                .ToList();

            if (pendingTasks.Count > 0 && attempt < MaxTaskAttempts)
            {
                _logger.LogWarning(
                    "Required-skill enrichment attempt {Attempt}/{MaxAttempts} left {MissingCount} tasks; retrying only those tasks.",
                    attempt,
                    MaxTaskAttempts,
                    pendingTasks.Count);
                await Task.Delay((attempt * 250) + Random.Shared.Next(50, 151), cancellationToken);
            }
        }

        var skillByNormalizedName = allSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill.NormalizedName))
            .GroupBy(skill => skill.NormalizedName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var newSkillsByNormalizedName = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
        var requiredSkillsToSave = new List<TaskRequiredSkill>();
        var processedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var persistedTaskIds = new HashSet<Guid>();

        foreach (var (taskId, generatedSkills) in generatedByTask)
        {
            foreach (var generatedSkill in generatedSkills)
            {
                if (!Enum.TryParse<SkillLevel>(generatedSkill.RequiredLevel, true, out var requiredLevel))
                    continue;

                var normalizedName = SkillNormalizer.Normalize(generatedSkill.SkillName);
                if (string.IsNullOrWhiteSpace(normalizedName))
                    continue;

                if (!skillByNormalizedName.TryGetValue(normalizedName, out var skillEntity))
                {
                    if (!newSkillsByNormalizedName.TryGetValue(normalizedName, out skillEntity))
                    {
                        skillEntity = new Skill
                        {
                            Name = generatedSkill.SkillName.Trim(),
                            NormalizedName = normalizedName
                        };
                        newSkillsByNormalizedName[normalizedName] = skillEntity;
                    }
                }

                if (!processedPairs.Add($"{taskId}_{normalizedName}"))
                    continue;

                requiredSkillsToSave.Add(new TaskRequiredSkill
                {
                    TaskId = taskId,
                    Skill = skillEntity,
                    RequiredLevel = requiredLevel
                });
                persistedTaskIds.Add(taskId);
            }
        }

        var warnings = eligibleTasks
            .Where(task => !task.RequiredSkills.Any() && !persistedTaskIds.Contains(task.Id))
            .Select(task => $"Task {task.Id}: {failureReasonByTask.GetValueOrDefault(task.Id, "INVALID_GENERATED_SKILL")}")
            .ToList();
        var totalEnriched = alreadyEnriched + persistedTaskIds.Count;
        var resultStats = new SkillEnrichmentResult
        {
            TasksProcessed = eligibleTasks.Count,
            TasksEnriched = totalEnriched,
            TasksSkipped = eligibleTasks.Count - totalEnriched,
            SkillsCreated = newSkillsByNormalizedName.Count,
            Warnings = warnings
        };

        try
        {
            if (newSkillsByNormalizedName.Count > 0)
                await _skillRepository.AddRangeAsync(newSkillsByNormalizedName.Values);

            if (requiredSkillsToSave.Count > 0)
                await _taskRequiredSkillRepository.AddRangeAsync(requiredSkillsToSave);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(resultStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist required-skill enrichment for project {ProjectId}.", projectId);
            return Result.Failure<SkillEnrichmentResult>(WbsErrors.RequiredSkillsPersistenceFailed);
        }
    }

    private async Task<IReadOnlyCollection<BatchResult>> RunBatchRoundAsync(
        IReadOnlyCollection<TaskItem> tasks,
        IReadOnlyCollection<string> availableSkillNames,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrentBatches, MaxConcurrentBatches);
        var batches = tasks.Chunk(BatchSize).Select(batch => batch.ToList()).ToList();

        var calls = batches.Select(async batch =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var inputs = batch.Select(task => new SkillEnrichmentTaskInput
                {
                    TaskId = task.Id,
                    Title = task.TitleEn,
                    Description = task.DescriptionEn ?? string.Empty
                }).ToList();

                var result = await _agent.EnrichBatchAsync(inputs, availableSkillNames, cancellationToken);
                return new BatchResult(batch, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(calls);
    }

    private sealed record BatchResult(
        IReadOnlyCollection<TaskItem> Tasks,
        Result<List<GeneratedTaskRequiredSkills>> Result);
}
