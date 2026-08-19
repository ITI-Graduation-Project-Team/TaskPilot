using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Assignment;
using TaskPilot.DTOs.Tasks;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Data.Repositories;

namespace TaskPilot.Services.Implementations
{
    public class TaskStatusService : ITaskStatusService
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ISprintRepository _sprintRepository;
        private readonly IProjectEmployeeRepository _projectEmployeeRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<TaskStatusService> _logger;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Company> _companyRepository;

        public TaskStatusService(
            ITaskRepository taskRepository,
            ISprintRepository sprintRepository,
            IProjectEmployeeRepository projectEmployeeRepository,
            INotificationService notificationService,
            ILogger<TaskStatusService> logger,
            IRepository<Project> projectRepository,
            IRepository<Company> companyRepository)
        {
            _taskRepository = taskRepository;
            _sprintRepository = sprintRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _notificationService = notificationService;
            _logger = logger;
            _projectRepository = projectRepository;
            _companyRepository = companyRepository;
        }

        public async Task<Result<MyTasksSummaryDto>> GetMyTasksAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty || currentUserId == Guid.Empty)
            {
                _logger.LogWarning("GetMyTasksAsync failed: Invalid input for ProjectId {ProjectId} or UserId {UserId}", projectId, currentUserId);
                return Result.Failure<MyTasksSummaryDto>(new Error("INVALID_INPUT", ErrorType.Validation, "Invalid project or user ID."));
            }

            var sprint = await _sprintRepository.GetActiveSprintByProjectIdAsync(projectId, cancellationToken);
            if (sprint == null || sprint.Status != SprintStatus.Active)
            {
                _logger.LogWarning("GetMyTasksAsync failed: Active sprint not found for ProjectId {ProjectId}", projectId);
                return Result.Failure<MyTasksSummaryDto>(TaskErrors.ActiveSprintNotFound);
            }

            var tasks = await _taskRepository.GetAssignedTasksBySprintAsync(sprint.Id, currentUserId, cancellationToken);

            var summary = new MyTasksSummaryDto
            {
                SprintId = sprint.Id,
                SprintTitleEn = sprint.TitleEn,
                DaysRemaining = sprint.EndDate >= DateTime.UtcNow ? (sprint.EndDate - DateTime.UtcNow).Days : 0,
                TotalTasks = tasks.Count,
                ToDoCount = tasks.Count(t => t.Status == TaskItemStatus.ToDo),
                InProgressCount = tasks.Count(t => t.Status == TaskItemStatus.InProgress),
                DoneCount = tasks.Count(t => t.Status == TaskItemStatus.Done),
                TotalEstimatedHours = tasks.Sum(t => t.EstimatedHours),
                TotalActualHours = tasks.Sum(t => t.ActualHours),
                Tasks = tasks.Select(t => new MyTaskDto
                {
                    TaskId = t.Id,
                    TitleEn = t.TitleEn,
                    TitleAr = t.TitleAr,
                    DescriptionEn = t.DescriptionEn,
                    DescriptionAr = t.DescriptionAr,
                    AcceptanceCriteriaEn = t.AcceptanceCriteriaEn,
                    AcceptanceCriteriaAr = t.AcceptanceCriteriaAr,
                    Priority = t.Priority,
                    Status = t.Status,
                    EffortSize = t.EffortSize,
                    EstimatedHours = t.EstimatedHours,
                    ActualHours = t.ActualHours,
                    Type = t.Type,
                    UserStoryTitleEn = t.UserStory?.TitleEn ?? string.Empty,
                    UserStoryTitleAr = t.UserStory?.TitleAr ?? string.Empty,
                    RequiredSkills = t.RequiredSkills.Select(rs => rs.Skill?.Name ?? string.Empty).ToList()
                }).ToList()
            };

            if (summary.TotalTasks > 0)
            {
                summary.CompletionPercentage = Math.Round(((decimal)summary.DoneCount / summary.TotalTasks) * 100, 2);
            }

            return Result.Success(summary);
        }

        public async Task<Result<TaskStatusUpdateResult>> UpdateStatusAsync(
            Guid taskId,
            Guid currentUserId,
            UpdateTaskStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty || request == null)
            {
                _logger.LogWarning("UpdateStatusAsync failed: Invalid input. TaskId {TaskId}, UserId {UserId}", taskId, currentUserId);
                return Result.Failure<TaskStatusUpdateResult>(new Error("INVALID_INPUT", ErrorType.Validation, "Invalid input."));
            }

            var task = await _taskRepository.GetByIdWithSprintAsync(taskId, cancellationToken);
            if (task == null)
            {
                _logger.LogWarning("UpdateStatusAsync failed: Task not found. TaskId {TaskId}", taskId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskNotFound);
            }

            if (task.Sprint == null || task.Sprint.Status != SprintStatus.Active)
            {
                _logger.LogWarning("UpdateStatusAsync failed: Sprint not active. TaskId {TaskId}", taskId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.SprintNotActive);
            }

            var isProjectManager = await _projectEmployeeRepository.IsProjectManagerAsync(task.Sprint.ProjectId, currentUserId, cancellationToken);
            if (task.EmployeeId != currentUserId && !isProjectManager)
            {
                _logger.LogWarning("UpdateStatusAsync failed: Authorization failure. TaskId {TaskId}, UserId {UserId}", taskId, currentUserId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.ForbiddenTaskUpdate);
            }

            if (task.Status == request.Status)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskAlreadyInRequestedStatus);
            }

            var transitionValidation = ValidateStatusTransition(task.Status, request.Status, isProjectManager);
            if (transitionValidation.IsFailure)
            {
                _logger.LogWarning("UpdateStatusAsync failed: Invalid transition from {CurrentStatus} to {RequestedStatus}. TaskId {TaskId}", task.Status, request.Status, taskId);
                return Result.Failure<TaskStatusUpdateResult>(transitionValidation.Error);
            }

            var previousStatus = task.Status;

            // Accumulate time if leaving InProgress
            if (previousStatus == TaskItemStatus.InProgress && request.Status != TaskItemStatus.InProgress)
            {
                if (task.InProgressAt.HasValue)
                {
                    decimal hoursPerDay = 8.0m;
                    int workingDaysMask = 62; // Default Mon-Fri
                    decimal allocationPercentage = 100m;

                    var capacityProject = await _projectRepository.GetByIdAsync(task.Sprint.ProjectId);
                    if (capacityProject != null)
                    {
                        var company = await _companyRepository.GetByIdAsync(capacityProject.CompanyId);
                        if (company != null)
                        {
                            hoursPerDay = company.WorkingHoursPerDay;
                            workingDaysMask = company.WorkingDaysMask;
                        }
                    }

                    if (task.EmployeeId.HasValue)
                    {
                        var projectEmployees = await _projectEmployeeRepository.GetActiveByProjectIdAsync(task.Sprint.ProjectId, cancellationToken);
                        var projectEmployee = projectEmployees.FirstOrDefault(pe => pe.EmployeeId == task.EmployeeId.Value);
                        if (projectEmployee != null)
                        {
                            allocationPercentage = projectEmployee.AllocationPercentage;
                        }
                    }

                    var rawHours = CalculateWorkingHours(task.InProgressAt.Value, DateTime.UtcNow, workingDaysMask, hoursPerDay);
                    task.ActualHours += Math.Round(rawHours * (allocationPercentage / 100m), 2);
                    task.InProgressAt = null;
                }
            }

            // Start time if entering InProgress
            if (request.Status == TaskItemStatus.InProgress)
            {
                task.InProgressAt = DateTime.UtcNow;
            }

            task.Status = request.Status;
            var shouldShowInSprintActivity = ShouldShowInSprintActivity(previousStatus, task.Status);
            var shouldNotifyReview = previousStatus != TaskItemStatus.Review && task.Status == TaskItemStatus.Review;
            Project? project = null;

            if (shouldShowInSprintActivity || shouldNotifyReview)
            {
                project = await _projectRepository.GetByIdAsync(task.Sprint.ProjectId);
            }

            if (shouldShowInSprintActivity)
            {
                _taskRepository.AddOverrideLog(new TaskStatusOverrideLog
                {
                    TaskId = task.Id,
                    PerformedByPmId = currentUserId,
                    FromStatus = previousStatus,
                    ToStatus = task.Status,
                    ReasonEn = $"Status changed from {previousStatus} to {task.Status}",
                    OverrideType = "StatusChange"
                });
            }

            _logger.LogInformation("Task {TaskId} in Project {ProjectId} (Sprint {SprintId}) status updated from {PreviousStatus} to {NewStatus} by {EmployeeId}. Actual hours: {ActualHours}", 
                task.Id, task.Sprint.ProjectId, task.SprintId, previousStatus.ToString(), task.Status.ToString(), currentUserId, task.ActualHours);

            if (shouldNotifyReview)
            {
                if (project != null && project.ManagerId != Guid.Empty)
                {
                    await _notificationService.SendAsync(
                        userId: project.ManagerId,
                        type: NotificationType.TaskUpdated,
                        messageEn: $"Task '{task.TitleEn}' is ready for review.",
                        messageAr: $"المهمة '{task.TitleAr ?? task.TitleEn}' جاهزة للمراجعة.",
                        url: $"/projects/{project.Id}/board/tasks/{task.Id}"
                    );
                }
            }

            var result = new TaskStatusUpdateResult
            {
                TaskId = task.Id,
                ProjectId = task.Sprint.ProjectId,
                SprintId = task.Sprint.Id,
                ProjectManagerId = project?.ManagerId,
                EmployeeId = task.EmployeeId,
                TitleEn = task.TitleEn,
                PreviousStatus = previousStatus,
                NewStatus = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours
            };

            return Result.Success(result);
        }

        public async Task<Result<TaskStatusUpdateResult>> PmRejectReviewAsync(
            Guid taskId,
            Guid currentUserId,
            PmRejectReviewRequest request,
            CancellationToken cancellationToken = default)
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty || request == null || (string.IsNullOrWhiteSpace(request.ReasonEn) && string.IsNullOrWhiteSpace(request.ReasonAr)))
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskOverrideReasonRequired);
            }

            var task = await _taskRepository.GetByIdWithSprintAsync(taskId, cancellationToken);
            if (task == null)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskNotFound);
            }

            if (task.Sprint == null || task.Sprint.Status != SprintStatus.Active)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.SprintNotActive);
            }

            var isProjectManager = await _projectEmployeeRepository.IsProjectManagerAsync(task.Sprint.ProjectId, currentUserId, cancellationToken);
            if (!isProjectManager)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.ForbiddenTaskUpdate);
            }

            if (task.Status != TaskItemStatus.Review)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.InvalidTaskStatusTransition);
            }

            var previousStatus = task.Status;
            task.Status = TaskItemStatus.InProgress;
            task.InProgressAt = DateTime.UtcNow; // Start the timer again for the developer

            var log = new TaskPilot.Models.Entities.TaskStatusOverrideLog
            {
                TaskId = task.Id,
                PerformedByPmId = currentUserId,
                FromStatus = previousStatus,
                ToStatus = task.Status,
                ReasonEn = request.ReasonEn,
                ReasonAr = request.ReasonAr,
                OverrideType = "ReviewReject"
            };

            _taskRepository.AddOverrideLog(log);
            var project = await _projectRepository.GetByIdAsync(task.Sprint.ProjectId);

            var result = new TaskStatusUpdateResult
            {
                TaskId = task.Id,
                ProjectId = task.Sprint.ProjectId,
                SprintId = task.Sprint.Id,
                ProjectManagerId = project?.ManagerId,
                EmployeeId = task.EmployeeId,
                TitleEn = task.TitleEn,
                PreviousStatus = previousStatus,
                NewStatus = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours
            };

            return Result.Success(result);
        }

        public async Task<Result<TaskStatusUpdateResult>> PmReopenTaskAsync(
            Guid taskId,
            Guid currentUserId,
            PmReopenTaskRequest request,
            CancellationToken cancellationToken = default)
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty || request == null || (string.IsNullOrWhiteSpace(request.ReasonEn) && string.IsNullOrWhiteSpace(request.ReasonAr)))
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskOverrideReasonRequired);
            }

            var task = await _taskRepository.GetByIdWithSprintAsync(taskId, cancellationToken);
            if (task == null)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskNotFound);
            }

            if (task.Sprint == null || task.Sprint.Status != SprintStatus.Active)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.SprintNotActive);
            }

            var isProjectManager = await _projectEmployeeRepository.IsProjectManagerAsync(task.Sprint.ProjectId, currentUserId, cancellationToken);
            if (!isProjectManager)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.ForbiddenTaskUpdate);
            }

            if (task.Status != TaskItemStatus.Done)
            {
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.InvalidTaskStatusTransition);
            }

            var previousStatus = task.Status;
            task.Status = TaskItemStatus.InProgress;
            task.InProgressAt = DateTime.UtcNow;

            var log = new TaskPilot.Models.Entities.TaskStatusOverrideLog
            {
                TaskId = task.Id,
                PerformedByPmId = currentUserId,
                FromStatus = previousStatus,
                ToStatus = task.Status,
                ReasonEn = request.ReasonEn,
                ReasonAr = request.ReasonAr,
                OverrideType = "Reopen"
            };

            _taskRepository.AddOverrideLog(log);
            var project = await _projectRepository.GetByIdAsync(task.Sprint.ProjectId);

            var result = new TaskStatusUpdateResult
            {
                TaskId = task.Id,
                ProjectId = task.Sprint.ProjectId,
                SprintId = task.Sprint.Id,
                ProjectManagerId = project?.ManagerId,
                EmployeeId = task.EmployeeId,
                TitleEn = task.TitleEn,
                PreviousStatus = previousStatus,
                NewStatus = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours
            };

            return Result.Success(result);
        }

        public async Task<Result<TaskStatusUpdateResult>> LogActualHoursAsync(
            Guid taskId,
            Guid currentUserId,
            LogActualHoursRequest request,
            CancellationToken cancellationToken = default)
        {
            if (taskId == Guid.Empty || currentUserId == Guid.Empty || request == null)
            {
                _logger.LogWarning("LogActualHoursAsync failed: Invalid input. TaskId {TaskId}, UserId {UserId}", taskId, currentUserId);
                return Result.Failure<TaskStatusUpdateResult>(new Error("INVALID_INPUT", ErrorType.Validation, "Invalid input."));
            }

            if (request.ActualHours <= 0)
            {
                _logger.LogWarning("LogActualHoursAsync failed: Invalid actual hours ({ActualHours}). TaskId {TaskId}", request.ActualHours, taskId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.InvalidActualHours);
            }

            var task = await _taskRepository.GetByIdWithSprintAsync(taskId, cancellationToken);
            if (task == null)
            {
                _logger.LogWarning("LogActualHoursAsync failed: Task not found. TaskId {TaskId}", taskId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.TaskNotFound);
            }

            if (task.Sprint == null || task.Sprint.Status != SprintStatus.Active)
            {
                _logger.LogWarning("LogActualHoursAsync failed: Sprint not active. TaskId {TaskId}", taskId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.SprintNotActive);
            }

            var isProjectManager = await _projectEmployeeRepository.IsProjectManagerAsync(task.Sprint.ProjectId, currentUserId, cancellationToken);
            if (task.EmployeeId != currentUserId && !isProjectManager)
            {
                _logger.LogWarning("LogActualHoursAsync failed: Authorization failure. TaskId {TaskId}, UserId {UserId}", taskId, currentUserId);
                return Result.Failure<TaskStatusUpdateResult>(TaskErrors.ForbiddenTaskUpdate);
            }

            task.ActualHours = request.ActualHours;

            _logger.LogInformation("Task {TaskId} in Project {ProjectId} (Sprint {SprintId}) actual hours updated to {ActualHours} by {EmployeeId}", 
                task.Id, task.Sprint.ProjectId, task.SprintId, task.ActualHours, currentUserId);

            var result = new TaskStatusUpdateResult
            {
                TaskId = task.Id,
                TitleEn = task.TitleEn,
                PreviousStatus = task.Status,
                NewStatus = task.Status,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours
            };

            return Result.Success(result);
        }

        public async Task<Result<MyTasksSummaryDto>> GetMySprintTasksAsync(
            Guid projectId,
            Guid sprintId,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty || sprintId == Guid.Empty || currentUserId == Guid.Empty)
            {
                _logger.LogWarning("GetMySprintTasksAsync failed: Invalid input for ProjectId {ProjectId}, SprintId {SprintId} or UserId {UserId}", projectId, sprintId, currentUserId);
                return Result.Failure<MyTasksSummaryDto>(new Error("INVALID_INPUT", ErrorType.Validation, "Invalid project, sprint or user ID."));
            }

            var sprint = await _sprintRepository.GetByIdAsync(sprintId);
            if (sprint == null || sprint.IsDeleted)
            {
                _logger.LogWarning("GetMySprintTasksAsync failed: Sprint not found for SprintId {SprintId}", sprintId);
                return Result.Failure<MyTasksSummaryDto>(SprintErrors.SprintNotFound);
            }

            if (sprint.ProjectId != projectId)
            {
                _logger.LogWarning("GetMySprintTasksAsync failed: Sprint {SprintId} does not belong to Project {ProjectId}", sprintId, projectId);
                return Result.Failure<MyTasksSummaryDto>(SprintErrors.SprintDoesNotBelongToProject);
            }

            if (sprint.Status != SprintStatus.Active)
            {
                _logger.LogWarning("GetMySprintTasksAsync failed: Sprint {SprintId} is not active", sprintId);
                return Result.Failure<MyTasksSummaryDto>(TaskErrors.SprintNotActive);
            }

            var tasks = await _taskRepository.GetAssignedTasksBySprintAsync(sprintId, currentUserId, cancellationToken);

            var summary = new MyTasksSummaryDto
            {
                SprintId = sprint.Id,
                SprintTitleEn = sprint.TitleEn,
                DaysRemaining = sprint.EndDate >= DateTime.UtcNow ? (sprint.EndDate - DateTime.UtcNow).Days : 0,
                TotalTasks = tasks.Count,
                ToDoCount = tasks.Count(t => t.Status == TaskItemStatus.ToDo),
                InProgressCount = tasks.Count(t => t.Status == TaskItemStatus.InProgress),
                DoneCount = tasks.Count(t => t.Status == TaskItemStatus.Done),
                TotalEstimatedHours = tasks.Sum(t => t.EstimatedHours),
                TotalActualHours = tasks.Sum(t => t.ActualHours),
                Tasks = tasks.Select(t => new MyTaskDto
                {
                    TaskId = t.Id,
                    TitleEn = t.TitleEn,
                    TitleAr = t.TitleAr,
                    DescriptionEn = t.DescriptionEn,
                    DescriptionAr = t.DescriptionAr,
                    AcceptanceCriteriaEn = t.AcceptanceCriteriaEn,
                    AcceptanceCriteriaAr = t.AcceptanceCriteriaAr,
                    Priority = t.Priority,
                    Status = t.Status,
                    EffortSize = t.EffortSize,
                    EstimatedHours = t.EstimatedHours,
                    ActualHours = t.ActualHours,
                    Type = t.Type,
                    UserStoryTitleEn = t.UserStory?.TitleEn ?? string.Empty,
                    UserStoryTitleAr = t.UserStory?.TitleAr ?? string.Empty,
                    RequiredSkills = t.RequiredSkills.Select(rs => rs.Skill?.Name ?? string.Empty).ToList()
                }).ToList()
            };

            if (summary.TotalTasks > 0)
            {
                summary.CompletionPercentage = Math.Round(((decimal)summary.DoneCount / summary.TotalTasks) * 100, 2);
            }

            return Result.Success(summary);
        }

        /*
        private static readonly IReadOnlyDictionary<TaskItemStatus, HashSet<TaskItemStatus>> _allowedTransitions = new Dictionary<TaskItemStatus, HashSet<TaskItemStatus>>
        {
            { TaskItemStatus.ToDo, new HashSet<TaskItemStatus> { TaskItemStatus.InProgress } },
            { TaskItemStatus.InProgress, new HashSet<TaskItemStatus> { TaskItemStatus.ToDo, TaskItemStatus.Review, TaskItemStatus.Done } },
            { TaskItemStatus.Review, new HashSet<TaskItemStatus> { TaskItemStatus.InProgress, TaskItemStatus.Done } },
            { TaskItemStatus.Done, new HashSet<TaskItemStatus> { TaskItemStatus.InProgress } }
        };
        */

        private decimal CalculateWorkingHours(DateTime start, DateTime end, int workingDaysMask, decimal hoursPerDay)
        {
            if (start >= end)
            {
                return 0;
            }

            // Default standard start time
            int startHour = 9;
            DateTime firstDay = start.Date;
            DateTime lastDay = end.Date;

            if (firstDay == lastDay)
            {
                if (IsWorkingDay(firstDay.DayOfWeek, workingDaysMask))
                {
                    return CalculateSingleDayHours(start, end, startHour, hoursPerDay);
                }
                return 0;
            }

            decimal totalHours = 0;

            if (IsWorkingDay(firstDay.DayOfWeek, workingDaysMask))
            {
                totalHours += CalculateSingleDayHours(start, firstDay.AddHours(startHour + (double)hoursPerDay), startHour, hoursPerDay);
            }

            int totalIntermediateDays = (lastDay - firstDay).Days - 1;
            if (totalIntermediateDays > 0)
            {
                int fullWeeks = totalIntermediateDays / 7;
                int remainingDays = totalIntermediateDays % 7;
                
                int workingDaysInWeek = CountWorkingDaysInWeek(workingDaysMask);
                int fullWorkingDaysCount = fullWeeks * workingDaysInWeek;
                
                DateTime currentDay = firstDay.AddDays(1);
                for (int i = 0; i < remainingDays; i++)
                {
                    if (IsWorkingDay(currentDay.DayOfWeek, workingDaysMask))
                    {
                        fullWorkingDaysCount++;
                    }
                    currentDay = currentDay.AddDays(1);
                }
                
                totalHours += fullWorkingDaysCount * hoursPerDay;
            }

            if (IsWorkingDay(lastDay.DayOfWeek, workingDaysMask))
            {
                totalHours += CalculateSingleDayHours(lastDay.AddHours(startHour), end, startHour, hoursPerDay);
            }

            return Math.Round(totalHours, 2);
        }

        private bool IsWorkingDay(DayOfWeek day, int mask)
        {
            return (mask & (1 << (int)day)) != 0;
        }

        private int CountWorkingDaysInWeek(int mask)
        {
            int count = 0;
            for (int i = 0; i < 7; i++)
            {
                if ((mask & (1 << i)) != 0) count++;
            }
            return count;
        }

        private decimal CalculateSingleDayHours(DateTime dayStart, DateTime dayEnd, int startHour, decimal hoursPerDay)
        {
            DateTime workStart = dayStart.Date.AddHours(startHour);
            DateTime workEnd = dayStart.Date.AddHours(startHour + (double)hoursPerDay);

            DateTime actualStart = dayStart > workStart ? dayStart : workStart;
            DateTime actualEnd = dayEnd < workEnd ? dayEnd : workEnd;

            return actualStart < actualEnd ? (decimal)(actualEnd - actualStart).TotalHours : 0;
        }


        private Result ValidateStatusTransition(TaskItemStatus current, TaskItemStatus requested, bool isProjectManager)
        {
            if (isProjectManager)
            {
                // PM can only move from Review to Done
                if (current == TaskItemStatus.Review && requested == TaskItemStatus.Done)
                    return Result.Success();

                if (requested == TaskItemStatus.Done)
                    return Result.Failure(TaskErrors.PmCannotCompleteTasks);

                return Result.Failure(TaskErrors.PmCannotStartTasks);
            }
            else
            {
                // Developer can move ToDo <-> InProgress, InProgress -> Review
                if (current == TaskItemStatus.ToDo && requested == TaskItemStatus.InProgress)
                    return Result.Success();
                if (current == TaskItemStatus.InProgress && requested == TaskItemStatus.ToDo)
                    return Result.Success();
                if (current == TaskItemStatus.InProgress && requested == TaskItemStatus.Review)
                    return Result.Success();
                    
                return Result.Failure(TaskErrors.InvalidTaskStatusTransition);
            }
        }

        private static bool ShouldShowInSprintActivity(TaskItemStatus fromStatus, TaskItemStatus toStatus)
        {
            return fromStatus != toStatus;
        }
    }
}
