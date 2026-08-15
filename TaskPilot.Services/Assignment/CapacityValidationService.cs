using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Constants;

namespace TaskPilot.Services.Assignment;

public class CapacityValidationService : ICapacityValidationService
{
    private readonly ITeamSnapshotService _teamSnapshotService;
    private readonly IOptions<AssignmentOptions> _options;
    private readonly ILogger<CapacityValidationService> _logger;

    public CapacityValidationService(
        ITeamSnapshotService teamSnapshotService,
        IOptions<AssignmentOptions> options,
        ILogger<CapacityValidationService> logger)
    {
        _teamSnapshotService = teamSnapshotService;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<CapacityValidationResult>> ValidateAsync(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var snapshotResult = await _teamSnapshotService.GetSnapshotAsync(projectId, sprintId, cancellationToken);

        if (snapshotResult.IsFailure)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Capacity validation failed for ProjectId: {ProjectId}, SprintId: {SprintId}. " +
                "ValidationDurationMs: {ValidationDurationMs}, FailureCode: {FailureCode}",
                projectId, sprintId, stopwatch.ElapsedMilliseconds, snapshotResult.Error!.Code);

            return Result.Failure<CapacityValidationResult>(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value!;
        var result = new CapacityValidationResult();

        // 1. Calculate Team Capacity
        result.TeamCapacityHours = snapshot.Team.Developers.Sum(d => d.RemainingHours);

        // 2. Calculate Required Hours
        result.RequiredHours = snapshot.UnassignedTasks.Sum(t => (double)t.EstimatedHours);

        // 3. Calculate Capacity Utilization
        if (result.TeamCapacityHours == 0)
        {
            result.CapacityUtilizationPercentage = 100.0;
        }
        else
        {
            result.CapacityUtilizationPercentage = Math.Round((result.RequiredHours / result.TeamCapacityHours) * 100, 2);
        }

        // 4. Hard Validation Rules (Blockers) -> MOVED TO WARNINGS
        // Rule 1: No Project Team
        if (snapshot.Team.Developers.Count == 0)
        {
            result.Warnings.Add(new CapacityWarningDto
            {
                Code = "NO_PROJECT_TEAM",
                ActualValue = 0,
                LimitValue = 1
            });
        }

        // Rule 2: No Unassigned Tasks
        if (snapshot.UnassignedTasks.Count == 0)
        {
            result.Warnings.Add(new CapacityWarningDto
            {
                Code = "NO_UNASSIGNED_TASKS",
                ActualValue = 0,
                LimitValue = 1
            });
        }

        // Rule 3 removed from blockers (now a warning)

        // 5. Warning Rules
        // Warning 1: High Utilization
        var options = _options.Value;
        if (result.CapacityUtilizationPercentage >= options.HighUtilizationThreshold)
        {
            result.Warnings.Add(new CapacityWarningDto
            {
                Code = "HIGH_UTILIZATION",
                ActualValue = result.CapacityUtilizationPercentage,
                LimitValue = options.HighUtilizationThreshold
            });
        }

        // Warning 3: Capacity Exceeded
        if (result.RequiredHours > result.TeamCapacityHours)
        {
            result.Warnings.Add(new CapacityWarningDto
            {
                Code = "HOURS_EXCEEDED",
                ActualValue = result.RequiredHours,
                LimitValue = result.TeamCapacityHours
            });
        }

        // Warning 2: High Task Count
        if (snapshot.Team.Developers.Count > 0)
        {
            double averageTasksPerDeveloper = (double)snapshot.UnassignedTasks.Count / snapshot.Team.Developers.Count;
            if (averageTasksPerDeveloper > options.RecommendedTasksPerDeveloper)
            {
                result.Warnings.Add(new CapacityWarningDto
                {
                    Code = "HIGH_TASK_COUNT",
                    ActualValue = averageTasksPerDeveloper,
                    LimitValue = options.RecommendedTasksPerDeveloper
                });
            }
        }

        // No blockers logic anymore.
        
        result.Blockers = new System.Collections.Generic.List<CapacityBlockerDto>();

        // 7. Final Decision
        result.CanProceed = result.Blockers.Count == 0;
        result.BlockersCount = result.Blockers.Count;
        result.WarningsCount = result.Warnings.Count;

        stopwatch.Stop();
        result.ValidationDurationMs = stopwatch.ElapsedMilliseconds;
        result.ValidationTimestampUtc = DateTime.UtcNow;
        result.ValidationVersion = AssignmentConstants.ValidationVersion;

        // 8. Logging
        _logger.LogInformation(
            "Capacity validation for ProjectId: {ProjectId}, SprintId: {SprintId}. " +
            "ValidationDurationMs: {ValidationDurationMs}, ValidationVersion: {ValidationVersion}, " +
            "CanProceed: {CanProceed}, BlockersCount: {BlockersCount}, WarningsCount: {WarningsCount}",
            projectId, sprintId, result.ValidationDurationMs, result.ValidationVersion,
            result.CanProceed, result.BlockersCount, result.WarningsCount);

        return Result.Success(result);
    }
}
