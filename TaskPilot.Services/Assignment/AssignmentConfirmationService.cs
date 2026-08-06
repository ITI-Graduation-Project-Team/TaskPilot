using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.External; // <-- تمت إضافة مسار خدمة جوجل
using TaskPilot.Services.Assignment;

namespace TaskPilot.Services.Assignment;

public class AssignmentConfirmationService : IAssignmentConfirmationService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectEmployeeRepository _projectEmployeeRepository;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly ICalenderService _calenderService;
    private readonly ILogger<AssignmentConfirmationService> _logger;

    // 1. إضافة خدمة جوجل هنا
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly ITeamSnapshotService _teamSnapshotService;

    public AssignmentConfirmationService(
        ITaskRepository taskRepository,
        IProjectEmployeeRepository projectEmployeeRepository,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ICalenderService calenderService,
        IGoogleCalendarService googleCalendarService, // <-- حقن الخدمة هنا
        ITeamSnapshotService teamSnapshotService,
        ILogger<AssignmentConfirmationService> logger)
    {
        _taskRepository = taskRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _calenderService = calenderService;
        _googleCalendarService = googleCalendarService; // <-- حفظ الخدمة في المتغير
        _teamSnapshotService = teamSnapshotService;
        _logger = logger;
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

        var validEmployeeIds = await _projectEmployeeRepository
            .GetEmployeeIdsByProjectAsync(projectId, cancellationToken);

        var sprintTasks = await _taskRepository
            .GetBySprintIdAsync(sprintId, cancellationToken);

        var sprintTaskMap = sprintTasks.ToDictionary(t => t.Id);

        var snapshotResult = await _teamSnapshotService.GetSnapshotAsync(projectId, sprintId, cancellationToken);
        var provisionalRemaining = new Dictionary<Guid, double>();
        var totalCapacities = new Dictionary<Guid, double>();

        if (snapshotResult.IsSuccess && snapshotResult.Value != null)
        {
            foreach (var dev in snapshotResult.Value.Team.Developers)
            {
                provisionalRemaining[dev.EmployeeId] = dev.RemainingHours;
                totalCapacities[dev.EmployeeId] = dev.RemainingHours;
            }
        }

        foreach (var assignment in request.Assignments)
        {
            if (!sprintTaskMap.TryGetValue(assignment.TaskId, out var task))
            {
                result.Skipped++;
                var warningTpl = _localizationService.GetString("assignment.warnings.taskNotFound");
                result.Warnings.Add(string.Format(warningTpl, assignment.TaskId, sprintId));                continue;
            }

            if (!validEmployeeIds.Contains(assignment.EmployeeId))
            {
                result.Skipped++;
                var warningTpl = _localizationService.GetString("assignment.warnings.employeeNotInProject");
                result.Warnings.Add(string.Format(warningTpl, assignment.EmployeeId));      
                continue;
            }

            if (task.EmployeeId.HasValue && task.EmployeeId != assignment.EmployeeId)
            {
                result.OverridesApplied++;
            }

            //.........................................................................
            // Capacity warning — not a block
            if (provisionalRemaining.ContainsKey(assignment.EmployeeId))
            {
                provisionalRemaining[assignment.EmployeeId] -= (double)task.EstimatedHours;

                if (provisionalRemaining[assignment.EmployeeId] < 0)
                {
                    // Using invariant English string as default if localization not set
                    var warnEn = $"Developer over capacity. Sprint capacity: {totalCapacities[assignment.EmployeeId]:F0}h, Assigned: {(totalCapacities[assignment.EmployeeId] - provisionalRemaining[assignment.EmployeeId]):F0}h.";
                    var warningTpl = _localizationService.GetString("assignment.warnings.insufficientCapacity") ?? warnEn;
                    
                    if (warningTpl.Contains("{0}"))
                    {
                        result.Warnings.Add(string.Format(warningTpl, task.TitleEn, provisionalRemaining[assignment.EmployeeId].ToString("F0"), task.EstimatedHours));
                    }
                    else
                    {
                        result.Warnings.Add(warnEn);
                    }
                }
            }

                //.....................................................................................

                // هنا يتم تعيين المهمة للموظف
                task.EmployeeId = assignment.EmployeeId;
            task.Status = TaskItemStatus.ToDo;
            result.AssignmentsConfirmed++;

            try
            {
                var eventTitle = $" TaskPilot: {task.TitleEn}";
                var eventDescription = $"You have a new task assigned.\nTitle: {task.TitleEn}\nEstimated Hours: {task.EstimatedHours}";

                var startTime = DateTime.UtcNow;
                var endTime = DateTime.UtcNow.AddHours(Math.Max(1, (double)task.EstimatedHours));

                await _googleCalendarService.AddEventToCalendarAsync(
                    assignment.EmployeeId,
                    eventTitle,
                    eventDescription,
                    startTime,
                    endTime
                );
            }
            catch (Exception ex)
            {
                // The assignment itself succeeds regardless of Google Calendar status.
                // This warning will appear in server logs to help diagnose Calendar issues.
                _logger.LogWarning(ex,
                    "Google Calendar event creation failed for employee {EmployeeId} on task '{TaskTitle}'. " +
                    "The employee may not have linked their Google Calendar yet.",
                    assignment.EmployeeId, task.TitleEn);
            }
        }

        return Result.Success(result);
    }
}


//using System;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using TaskPilot.Data.Repositories;
//using TaskPilot.Data.Repositories.Interfaces;
//using TaskPilot.DTOs.Assignment;
//using TaskPilot.Models.Common;
//using TaskPilot.Models.Common.Results;
//using TaskPilot.Models.Enums;
//using TaskPilot.Services.Interfaces;

//namespace TaskPilot.Services.Assignment;

//public class AssignmentConfirmationService : IAssignmentConfirmationService
//{
//    private readonly ITaskRepository _taskRepository;
//    private readonly IProjectEmployeeRepository _projectEmployeeRepository;
//    private readonly ILocalizationService _localizationService;
//    private readonly INotificationService _notificationService;
//    private readonly ICalenderService _calenderService;

//    public AssignmentConfirmationService(
//        ITaskRepository taskRepository,
//        IProjectEmployeeRepository projectEmployeeRepository,
//        ILocalizationService localizationService,
//        INotificationService notificationService,
//        ICalenderService calenderService)
//    {
//        _taskRepository = taskRepository;
//        _projectEmployeeRepository = projectEmployeeRepository;
//        _localizationService = localizationService;
//        _notificationService = notificationService;
//        _calenderService = calenderService;
//    }

//    public async Task<Result<AssignmentConfirmationResult>> ConfirmAsync(
//        Guid projectId,
//        Guid sprintId,
//        ConfirmAssignmentsRequest request,
//        CancellationToken cancellationToken = default)
//    {
//        var result = new AssignmentConfirmationResult
//        {
//            TotalRequested = request.Assignments.Count
//        };

//        if (!request.Assignments.Any())
//        {
//            result.Warnings.Add(_localizationService.GetString("assignment.warnings.noAssignmentsProvided"));
//            return Result.Success(result);
//        }

//        // 1. Load all valid employee IDs for this project once
//        var validEmployeeIds = await _projectEmployeeRepository
//            .GetEmployeeIdsByProjectAsync(projectId, cancellationToken);

//        // 2. Load all task IDs for this sprint once
//        var sprintTasks = await _taskRepository
//            .GetBySprintIdAsync(sprintId, cancellationToken);

//        var sprintTaskMap = sprintTasks.ToDictionary(t => t.Id);

//        // 3. Process each assignment
//        foreach (var assignment in request.Assignments)
//        {
//            // Validate task belongs to this sprint
//            if (!sprintTaskMap.TryGetValue(assignment.TaskId, out var task))
//            {
//                result.Skipped++;
//                var warningTpl = _localizationService.GetString("assignment.warnings.taskNotFound");
//                result.Warnings.Add(string.Format(warningTpl, assignment.TaskId, sprintId));
//                continue;
//            }

//            // Validate employee belongs to this project
//            if (!validEmployeeIds.Contains(assignment.EmployeeId))
//            {
//                result.Skipped++;
//                var warningTpl = _localizationService.GetString("assignment.warnings.employeeNotInProject");
//                result.Warnings.Add(string.Format(warningTpl, assignment.EmployeeId));
//                continue;
//            }

//            // Track override
//            if (task.EmployeeId.HasValue && task.EmployeeId != assignment.EmployeeId)
//            {
//                result.OverridesApplied++;
//            }

//            // Capacity warning — not a block
//            var employeeCurrentHours = sprintTasks
//                .Where(t => t.EmployeeId == assignment.EmployeeId)
//                .Sum(t => (double)t.EstimatedHours);

//            // This is a rough check using already-loaded data
//            var maxSprintHours = 84.0; // default 14d × 6h
//            var remaining = maxSprintHours - employeeCurrentHours;

//            if (remaining < (double)task.EstimatedHours)
//            {
//                var warningTpl = _localizationService.GetString("assignment.warnings.insufficientCapacity");
//                result.Warnings.Add(string.Format(warningTpl, task.TitleEn, remaining.ToString("F0"), task.EstimatedHours));
//            }

//            // Apply assignment (no SaveChangesAsync here!)
//            task.EmployeeId = assignment.EmployeeId;

//            // Transition task status to ToDo upon assignment confirmation (ready to start)
//            task.Status = TaskItemStatus.ToDo;

//            result.AssignmentsConfirmed++;

//        }

//        return Result.Success(result);
//    }
//}
