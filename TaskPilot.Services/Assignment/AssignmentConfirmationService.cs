using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public class AssignmentConfirmationService : IAssignmentConfirmationService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectEmployeeRepository _projectEmployeeRepository;
    private readonly ILocalizationService _localizationService;

    public AssignmentConfirmationService(
        ITaskRepository taskRepository,
        IProjectEmployeeRepository projectEmployeeRepository,
        ILocalizationService localizationService)
    {
        _taskRepository = taskRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _localizationService = localizationService;
    }

    public async Task<Result<AssignmentConfirmationResult>> ConfirmAsync(
        Guid projectId,
        Guid sprintId,
        ConfirmAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new AssignmentConfirmationResult
        {
            TotalRequested = request.Assignments.Count
        };

        if (!request.Assignments.Any())
        {
            result.Warnings.Add(_localizationService.GetString("assignment.warnings.noAssignmentsProvided"));
            return Result.Success(result);
        }

        // 1. Load all valid employee IDs for this project once
        var validEmployeeIds = await _projectEmployeeRepository
            .GetEmployeeIdsByProjectAsync(projectId, cancellationToken);

        // 2. Load all task IDs for this sprint once
        var sprintTasks = await _taskRepository
            .GetBySprintIdAsync(sprintId, cancellationToken);

        var sprintTaskMap = sprintTasks.ToDictionary(t => t.Id);

        // 3. Process each assignment
        foreach (var assignment in request.Assignments)
        {
            // Validate task belongs to this sprint
            if (!sprintTaskMap.TryGetValue(assignment.TaskId, out var task))
            {
                result.Skipped++;
                var warningTpl = _localizationService.GetString("assignment.warnings.taskNotFound");
                result.Warnings.Add(string.Format(warningTpl, assignment.TaskId, sprintId));
                continue;
            }

            // Validate employee belongs to this project
            if (!validEmployeeIds.Contains(assignment.EmployeeId))
            {
                result.Skipped++;
                var warningTpl = _localizationService.GetString("assignment.warnings.employeeNotInProject");
                result.Warnings.Add(string.Format(warningTpl, assignment.EmployeeId));
                continue;
            }

            // Track override
            if (task.EmployeeId.HasValue && task.EmployeeId != assignment.EmployeeId)
            {
                result.OverridesApplied++;
            }

            // Capacity warning — not a block
            var employeeCurrentHours = sprintTasks
                .Where(t => t.EmployeeId == assignment.EmployeeId)
                .Sum(t => (double)t.EstimatedHours);

            // This is a rough check using already-loaded data
            var maxSprintHours = 84.0; // default 14d × 6h
            var remaining = maxSprintHours - employeeCurrentHours;

            if (remaining < (double)task.EstimatedHours)
            {
                var warningTpl = _localizationService.GetString("assignment.warnings.insufficientCapacity");
                result.Warnings.Add(string.Format(warningTpl, task.TitleEn, remaining.ToString("F0"), task.EstimatedHours));
            }

            // Apply assignment (no SaveChangesAsync here!)
            task.EmployeeId = assignment.EmployeeId;
            result.AssignmentsConfirmed++;
        }

        return Result.Success(result);
    }
}
