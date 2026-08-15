using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public class AssignmentScoringService : IAssignmentScoringService
{
    private readonly ITeamSnapshotService _teamSnapshotService;
    private readonly ICapacityValidationService _capacityValidationService;
    private readonly IOptions<ScoringWeights> _weightsOptions;
    private readonly IEnumerable<IScoreCalculator> _calculators;

    public AssignmentScoringService(
        ITeamSnapshotService teamSnapshotService,
        ICapacityValidationService capacityValidationService,
        IOptions<ScoringWeights> weightsOptions,
        IEnumerable<IScoreCalculator> calculators)
    {
        _teamSnapshotService = teamSnapshotService;
        _capacityValidationService = capacityValidationService;
        _weightsOptions = weightsOptions;
        _calculators = calculators;
    }

    public async Task<Result<ScoredAssignmentDto>> ScoreAsync(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        // Snapshot is retrieved early to validate project and sprint existence.
        // It's also required to get the snapshot data itself.
        var snapshotResult = await _teamSnapshotService.GetSnapshotAsync(projectId, sprintId, cancellationToken);
        
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

        var capacityResult = await _capacityValidationService.ValidateAsync(projectId, sprintId, cancellationToken);
        if (capacityResult.IsFailure)
        {
            return Result.Failure<ScoredAssignmentDto>(capacityResult.Error!);
        }

        if (!capacityResult.Value!.CanProceed)
        {
            return Result.Failure<ScoredAssignmentDto>(AssignmentErrors.CapacityValidationFailed);
        }

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
            TaskScores = new List<TaskScoringResultDto>()
        };

        var skillCalculator = _calculators.FirstOrDefault(c => c is SkillScoreCalculator);
        var availabilityCalculator = _calculators.FirstOrDefault(c => c is AvailabilityScoreCalculator);
        var velocityCalculator = _calculators.FirstOrDefault(c => c is VelocityScoreCalculator);
        var experienceCalculator = _calculators.FirstOrDefault(c => c is ExperienceScoreCalculator);

        var provisionalRemaining = snapshot.Team.Developers.ToDictionary(d => d.EmployeeId, d => d.RemainingHours);

        foreach (var task in snapshot.UnassignedTasks)
        {
            var taskScoringResult = new TaskScoringResultDto
            {
                Task = task,
                RankedDevelopers = new List<DeveloperScoreDto>()
            };

            foreach (var developer in snapshot.Team.Developers)
            {
                var currentRemaining = provisionalRemaining[developer.EmployeeId];
                if (currentRemaining <= 0.01)
                {
                    continue; // Hard exclusion
                }

                // Update the snapshot object so calculators receive the current capacity
                developer.RemainingHours = currentRemaining;

                var skillScore = skillCalculator?.Calculate(task, developer) ?? 0;
                var availabilityScore = availabilityCalculator?.Calculate(task, developer) ?? 0;
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

                taskScoringResult.RankedDevelopers.Add(new DeveloperScoreDto
                {
                    EmployeeId = developer.EmployeeId,
                    FullName = developer.FullName,
                    JobTitle = developer.JobTitle,
                    SkillScore = skillScore,
                    AvailabilityScore = availabilityScore,
                    VelocityScore = velocityScore,
                    ExperienceScore = experienceScore,
                    FinalScore = finalScore,
                    SkillGaps = skillGaps,
                    RemainingHours = currentRemaining,
                    MaxSprintHours = developer.MaxSprintHours,
                    CurrentAssignedHours = developer.CurrentAssignedHours,
                    HasSufficientCapacity = currentRemaining >= (double)task.EstimatedHours
                });
            }

            taskScoringResult.RankedDevelopers = taskScoringResult.RankedDevelopers
                .OrderByDescending(d => d.FinalScore)
                .ThenByDescending(d => d.RemainingHours)
                // Developer object is no longer available, so we omit HistoricalVelocity sorting
                // or we could add it to DeveloperScoreDto. We'll just omit it here.

                .ToList();

            // Decay capacity for the selected top developer
            var topDeveloper = taskScoringResult.RankedDevelopers.FirstOrDefault();
            if (topDeveloper != null)
            {
                provisionalRemaining[topDeveloper.EmployeeId] -= (double)task.EstimatedHours;
            }
            else
            {
                taskScoringResult.IsUnassignable = true;
            }

            scoredAssignment.TaskScores.Add(taskScoringResult);
        }

        return Result.Success(scoredAssignment);
    }
}
