using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public class AssignmentScoringService : IAssignmentScoringService
{
    private readonly ITeamSnapshotService _teamSnapshotService;
    private readonly IOptions<ScoringWeights> _weightsOptions;
    private readonly IEnumerable<IScoreCalculator> _calculators;
    private readonly ILogger<AssignmentScoringService> _logger;

    public AssignmentScoringService(
        ITeamSnapshotService teamSnapshotService,
        IOptions<ScoringWeights> weightsOptions,
        IEnumerable<IScoreCalculator> calculators,
        ILogger<AssignmentScoringService> logger)
    {
        _teamSnapshotService = teamSnapshotService;
        _weightsOptions = weightsOptions;
        _calculators = calculators;
        _logger = logger;
    }

    public async Task<Result<ScoredAssignmentDto>> ScoreAsync(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        // Snapshot is retrieved early to validate project and sprint existence.
        // It's also required to get the snapshot data itself.
        var snapshotResult = await _teamSnapshotService.GetSnapshotAsync(projectId, sprintId, cancellationToken);
        var snapshotMs = stopwatch.ElapsedMilliseconds;
        
        if (snapshotResult.IsFailure)
        {
            // Map common errors if needed, but TeamSnapshotService already returns appropriate errors
            // such as ProjectNotFound, SprintNotFound, SprintDoesNotBelongToProject
            if (snapshotResult.Error == AssignmentErrors.ProjectNotFound)
                return Result.Failure<ScoredAssignmentDto>(AssignmentErrors.InvalidProject);
                
            if (snapshotResult.Error == AssignmentErrors.SprintNotFound)
                return Result.Failure<ScoredAssignmentDto>(AssignmentErrors.InvalidSprint);

            return Result.Failure<ScoredAssignmentDto>(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value;
        if (snapshot == null)
        {
            return Result.Failure<ScoredAssignmentDto>(AssignmentErrors.SnapshotNotFound);
        }

        if (snapshot.SprintStatus != TaskPilot.Models.Enums.SprintStatus.Planned)
            return Result.Failure<ScoredAssignmentDto>(AssignmentErrors.SprintNotPlanned);

        var weightsValidation = _weightsOptions.Value.Validate();
        if (weightsValidation.IsFailure)
        {
            return Result.Failure<ScoredAssignmentDto>(weightsValidation.Error!);
        }

        var weights = _weightsOptions.Value;
        var scoredAssignment = new ScoredAssignmentDto
        {
            ProjectId = projectId,
            SprintId = sprintId,
            Weights = new ScoringWeightsDto
            {
                SkillWeight = weights.SkillWeight,
                AvailabilityWeight = weights.AvailabilityWeight,
                VelocityWeight = weights.VelocityWeight,
                ExperienceWeight = weights.ExperienceWeight
            },
            TaskScores = new List<TaskScoringResultDto>()
        };

        var skillCalculator = _calculators.FirstOrDefault(c => c is SkillScoreCalculator);
        var velocityCalculator = _calculators.FirstOrDefault(c => c is VelocityScoreCalculator);
        var experienceCalculator = _calculators.FirstOrDefault(c => c is ExperienceScoreCalculator);

        var editableHoursByDeveloper = snapshot.UnassignedTasks
            .Where(t => t.AssigneeId.HasValue)
            .GroupBy(t => t.AssigneeId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(t => (double)t.EstimatedHours));

        foreach (var task in snapshot.UnassignedTasks
                     .OrderByDescending(t => t.Priority)
                     .ThenByDescending(t => t.EstimatedHours))
        {
            var taskScoringResult = new TaskScoringResultDto
            {
                Task = task,
                RankedDevelopers = new List<DeveloperScoreDto>()
            };

            foreach (var developer in snapshot.Team.Developers)
            {
                var assignedWithoutTask = developer.CurrentAssignedHours;
                if (task.AssigneeId.HasValue && task.AssigneeId == developer.EmployeeId)
                {
                    assignedWithoutTask -= (double)task.EstimatedHours;
                }

                assignedWithoutTask = Math.Max(0, assignedWithoutTask);
                var projectedAssignedHours = assignedWithoutTask + (double)task.EstimatedHours;
                var projectedRemainingHours = developer.MaxSprintHours - projectedAssignedHours;
                var availabilityScore = developer.MaxSprintHours > 0
                    ? Math.Clamp(projectedRemainingHours / developer.MaxSprintHours * 100, 0, 100)
                    : 0;

                var skillScore = skillCalculator?.Calculate(task, developer) ?? 0;
                var velocityScore = velocityCalculator?.Calculate(task, developer) ?? 0;
                var experienceScore = experienceCalculator?.Calculate(task, developer) ?? 0;

                var finalScore = (skillScore * weights.SkillWeight +
                                  availabilityScore * weights.AvailabilityWeight +
                                  velocityScore * weights.VelocityWeight +
                                  experienceScore * weights.ExperienceWeight) / 100.0;

                finalScore = Math.Clamp(finalScore, 0, 100);
                finalScore = Math.Round(finalScore, 2);

                var skillGaps = new List<SkillGapDto>();
                foreach (var requiredSkill in task.RequiredSkills)
                {
                    var requiredNormalized = TaskPilot.Services.Helpers.SkillNormalizer.Normalize(requiredSkill.SkillName);
                    var devSkill = developer.Skills.FirstOrDefault(s => 
                        (s.SkillId > 0 && s.SkillId == requiredSkill.SkillId) ||
                        TaskPilot.Services.Helpers.SkillNormalizer.Normalize(s.SkillName) == requiredNormalized ||
                        requiredSkill.Aliases.Any(a => TaskPilot.Services.Helpers.SkillNormalizer.Normalize(a) == TaskPilot.Services.Helpers.SkillNormalizer.Normalize(s.SkillName))
                    );

                    if (devSkill == null || devSkill.Level < requiredSkill.RequiredLevel)
                    {
                        skillGaps.Add(new SkillGapDto
                        {
                            SkillId = requiredSkill.SkillId,
                            SkillName = requiredSkill.SkillName,
                            Reason = devSkill == null ? $"Missing {requiredSkill.SkillName} experience." : $"Skill level {devSkill.Level} is below required {requiredSkill.RequiredLevel}."
                        });
                    }
                }

                var editableHours = editableHoursByDeveloper.GetValueOrDefault(developer.EmployeeId);
                var nonEditableHours = Math.Max(0, developer.CurrentAssignedHours - editableHours);

                taskScoringResult.RankedDevelopers.Add(new DeveloperScoreDto
                {
                    EmployeeId = developer.EmployeeId,
                    FullName = developer.FullName,
                    JobTitle = developer.JobTitle,
                    SkillScore = skillScore,
                    AvailabilityScore = availabilityScore,
                    VelocityScore = velocityScore,
                    HasHistoricalData = developer.HasHistoricalData,
                    ExperienceScore = experienceScore,
                    FinalScore = finalScore,
                    SkillGaps = skillGaps,
                    RemainingHours = projectedRemainingHours,
                    MaxSprintHours = developer.MaxSprintHours,
                    CurrentAssignedHours = developer.CurrentAssignedHours,
                    NonEditableHours = nonEditableHours,
                    MatchedSkillsCount = Math.Max(0, task.RequiredSkills.Count - skillGaps.Count),
                    RequiredSkillsCount = task.RequiredSkills.Count,
                    HasSufficientCapacity = projectedRemainingHours >= 0
                });
            }

            taskScoringResult.RankedDevelopers = taskScoringResult.RankedDevelopers
                .OrderByDescending(d => d.FinalScore)
                .ThenByDescending(d => d.RemainingHours)
                .ToList();

            taskScoringResult.IsUnassignable = taskScoringResult.RankedDevelopers.Count == 0;

            scoredAssignment.TaskScores.Add(taskScoringResult);
        }

        _logger.LogInformation(
            "Assignment scoring completed for ProjectId: {ProjectId}, SprintId: {SprintId}. Tasks: {TaskCount}, Developers: {DeveloperCount}, SnapshotMs: {SnapshotMs}, ScoringMs: {ScoringMs}, DurationMs: {DurationMs}",
            projectId,
            sprintId,
            snapshot.UnassignedTasks.Count,
            snapshot.Team.Developers.Count,
            snapshotMs,
            stopwatch.ElapsedMilliseconds - snapshotMs,
            stopwatch.ElapsedMilliseconds);

        return Result.Success(scoredAssignment);
    }
}
