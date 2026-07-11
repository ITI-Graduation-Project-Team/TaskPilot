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

        foreach (var task in snapshot.UnassignedTasks)
        {
            var taskScoringResult = new TaskScoringResultDto
            {
                Task = task,
                RankedDevelopers = new List<DeveloperScoreDto>()
            };

            foreach (var developer in snapshot.Team.Developers)
            {
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
                    var devSkill = developer.Skills.FirstOrDefault(s => s.SkillName.Equals(requiredSkill.SkillName, StringComparison.OrdinalIgnoreCase));
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
                    Developer = developer,
                    SkillScore = skillScore,
                    AvailabilityScore = availabilityScore,
                    VelocityScore = velocityScore,
                    ExperienceScore = experienceScore,
                    FinalScore = finalScore,
                    SkillGaps = skillGaps
                });
            }

            taskScoringResult.RankedDevelopers = taskScoringResult.RankedDevelopers
                .OrderByDescending(d => d.FinalScore)
                .ThenByDescending(d => d.Developer.RemainingHours)
                .ThenByDescending(d => d.Developer.HistoricalVelocity ?? 0)
                .ToList();

            scoredAssignment.TaskScores.Add(taskScoringResult);
        }

        return Result.Success(scoredAssignment);
    }
}
