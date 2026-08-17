using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Services.Assignment;

public class AssignmentConfirmationService : IAssignmentConfirmationService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectEmployeeRepository _projectEmployeeRepository;
    private readonly IRepository<ProjectEmployee> _projectEmployeeEntityRepository;
    private readonly IRepository<TaskItem> _taskEntityRepository;
    private readonly IRepository<Sprint> _sprintRepository;
    private readonly ILocalizationService _localizationService;
    private readonly ITeamSnapshotService _teamSnapshotService;

    public AssignmentConfirmationService(
        ITaskRepository taskRepository,
        IProjectEmployeeRepository projectEmployeeRepository,
        IRepository<ProjectEmployee> projectEmployeeEntityRepository,
        IRepository<TaskItem> taskEntityRepository,
        IRepository<Sprint> sprintRepository,
        ILocalizationService localizationService,
        ITeamSnapshotService teamSnapshotService)
    {
        _taskRepository = taskRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _projectEmployeeEntityRepository = projectEmployeeEntityRepository;
        _taskEntityRepository = taskEntityRepository;
        _sprintRepository = sprintRepository;
        _localizationService = localizationService;
        _teamSnapshotService = teamSnapshotService;
    }

    public async Task<Result<AssignmentConfirmationResult>> ConfirmAsync(
        Guid projectId,
        Guid sprintId,
        ConfirmAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var sprintResult = await GetPlannedSprintAsync(projectId, sprintId);
        if (sprintResult.IsFailure)
            return Result.Failure<AssignmentConfirmationResult>(sprintResult.Error!);

        if (request.Assignments.GroupBy(a => a.TaskId).Any(g => g.Count() > 1))
            return Result.Failure<AssignmentConfirmationResult>(CommonErrors.InvalidInput("Duplicate task assignments are not allowed."));

        var result = new AssignmentConfirmationResult { TotalRequested = request.Assignments.Count };
        if (request.Assignments.Count == 0)
        {
            result.Warnings.Add(_localizationService.GetString("assignment.warnings.noAssignmentsProvided"));
            return Result.Success(result);
        }

        var validEmployeeIds = await _projectEmployeeRepository.GetEmployeeIdsByProjectAsync(projectId, cancellationToken);
        var sprintTasks = await _taskRepository.GetBySprintIdAsync(sprintId, cancellationToken);
        var sprintTaskMap = sprintTasks.ToDictionary(t => t.Id);

        var snapshotResult = await _teamSnapshotService.GetSnapshotAsync(projectId, sprintId, cancellationToken);
        if (snapshotResult.IsFailure)
            return Result.Failure<AssignmentConfirmationResult>(snapshotResult.Error!);

        var developers = snapshotResult.Value!.Team.Developers.ToDictionary(d => d.EmployeeId);
        var provisionalRemaining = developers.ToDictionary(x => x.Key, x => x.Value.RemainingHours);
        var changes = new List<(TaskItem Task, Guid? EmployeeId)>();
        var affectedEmployees = new HashSet<Guid>();

        foreach (var assignment in request.Assignments)
        {
            if (!sprintTaskMap.TryGetValue(assignment.TaskId, out var task))
            {
                result.Skipped++;
                var warning = _localizationService.GetString("assignment.warnings.taskNotFound");
                result.Warnings.Add(string.Format(warning, assignment.TaskId, sprintId));
                continue;
            }

            if (assignment.EmployeeId.HasValue && !validEmployeeIds.Contains(assignment.EmployeeId.Value))
            {
                result.Skipped++;
                var warning = _localizationService.GetString("assignment.warnings.employeeNotInProject");
                result.Warnings.Add(string.Format(warning, assignment.EmployeeId.Value));
                continue;
            }

            if (assignment.EmployeeId.HasValue && !developers.ContainsKey(assignment.EmployeeId.Value))
            {
                result.Skipped++;
                var warning = _localizationService.GetString("assignment.warnings.employeeNotInProject");
                result.Warnings.Add(string.Format(warning, assignment.EmployeeId.Value));
                continue;
            }

            if (task.EmployeeId == assignment.EmployeeId)
                continue;

            if (task.EmployeeId.HasValue && provisionalRemaining.ContainsKey(task.EmployeeId.Value))
            {
                provisionalRemaining[task.EmployeeId.Value] += (double)task.EstimatedHours;
                result.OverridesApplied++;
            }

            if (assignment.EmployeeId.HasValue && provisionalRemaining.ContainsKey(assignment.EmployeeId.Value))
            {
                provisionalRemaining[assignment.EmployeeId.Value] -= (double)task.EstimatedHours;
                affectedEmployees.Add(assignment.EmployeeId.Value);
            }

            changes.Add((task, assignment.EmployeeId));
            result.AssignmentsConfirmed++;
        }

        var overCapacityEmployees = affectedEmployees
            .Where(id => provisionalRemaining[id] < 0)
            .ToList();

        if (overCapacityEmployees.Count > 0 && !request.AllowOverCapacity)
            return Result.Failure<AssignmentConfirmationResult>(AssignmentErrors.CapacityExceeded);

        foreach (var employeeId in overCapacityEmployees)
        {
            var developer = developers[employeeId];
            var assignedHours = developer.MaxSprintHours - provisionalRemaining[employeeId];
            result.Warnings.Add($"{developer.FullName} is over capacity: {assignedHours:F0}h assigned of {developer.MaxSprintHours:F0}h.");
        }

        foreach (var change in changes)
            change.Task.EmployeeId = change.EmployeeId;

        return Result.Success(result);
    }

    public async Task<Result<AssignTaskResult>> AssignTaskAsync(
        Guid projectId,
        Guid sprintId,
        Guid taskId,
        AssignTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var sprintResult = await GetPlannedSprintAsync(projectId, sprintId);
        if (sprintResult.IsFailure)
            return Result.Failure<AssignTaskResult>(sprintResult.Error!);

        var sprint = sprintResult.Value;

        var task = await _taskRepository.GetByIdWithSprintAsync(taskId, cancellationToken);
        if (task == null || task.SprintId != sprintId)
            return Result.Failure<AssignTaskResult>(CommonErrors.NotFound("Task"));

        var previousEmployeeId = task.EmployeeId;
        if (previousEmployeeId == request.EmployeeId)
        {
            return Result.Success(new AssignTaskResult
            {
                TaskId = task.Id,
                PreviousEmployeeId = previousEmployeeId,
                EmployeeId = request.EmployeeId
            });
        }

        var result = new AssignTaskResult
        {
            TaskId = task.Id,
            PreviousEmployeeId = previousEmployeeId,
            EmployeeId = request.EmployeeId
        };

        if (request.EmployeeId.HasValue)
        {
            var projectEmployee = await _projectEmployeeEntityRepository.GetQueryable()
                .AsNoTracking()
                .Include(pe => pe.Employee)
                .Include(pe => pe.Project)
                    .ThenInclude(project => project.Company)
                .FirstOrDefaultAsync(pe =>
                    pe.ProjectId == projectId &&
                    pe.EmployeeId == request.EmployeeId.Value &&
                    pe.IsActive &&
                    !pe.Employee.IsDeactivated,
                    cancellationToken);

            if (projectEmployee == null)
                return Result.Failure<AssignTaskResult>(CommonErrors.InvalidInput("The selected employee is not an active member of this project."));

            var currentAssignedHours = await _taskEntityRepository.GetQueryable()
                .AsNoTracking()
                .Where(t => t.SprintId == sprintId && t.EmployeeId == request.EmployeeId.Value)
                .SumAsync(t => (decimal?)t.EstimatedHours, cancellationToken) ?? 0m;
            var maxSprintHours = AssignmentCapacityCalculator.CalculateMaxSprintHours(
                sprint,
                projectEmployee.Project.Company,
                projectEmployee.AllocationPercentage);
            var projectedAssignedHours = (double)(currentAssignedHours + task.EstimatedHours);
            result.AssignedHours = projectedAssignedHours;
            result.MaxSprintHours = maxSprintHours;

            if (projectedAssignedHours > maxSprintHours)
            {
                if (!request.AllowOverCapacity)
                    return Result.Failure<AssignTaskResult>(AssignmentErrors.CapacityExceeded);

                var fullName = $"{projectEmployee.Employee.FirstNameEn} {projectEmployee.Employee.LastNameEn}".Trim();
                result.Warnings.Add($"{fullName} will be over capacity: {projectedAssignedHours:F0}h assigned of {maxSprintHours:F0}h.");
            }
        }

        task.EmployeeId = request.EmployeeId;
        result.Changed = true;
        return Result.Success(result);
    }

    private async Task<Result<Sprint>> GetPlannedSprintAsync(Guid projectId, Guid sprintId)
    {
        var sprint = await _sprintRepository.GetByIdAsync(sprintId);
        if (sprint == null)
            return Result.Failure<Sprint>(AssignmentErrors.SprintNotFound);
        if (sprint.ProjectId != projectId)
            return Result.Failure<Sprint>(AssignmentErrors.SprintDoesNotBelongToProject);
        if (sprint.Status != SprintStatus.Planned)
            return Result.Failure<Sprint>(AssignmentErrors.SprintNotPlanned);

        return Result.Success(sprint);
    }
}
