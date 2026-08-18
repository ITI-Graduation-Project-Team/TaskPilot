using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Sprints;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.External;
using TaskPilot.Services.BackgroundJobs;
using TaskPilot.Models.Common;
using Hangfire;
namespace TaskPilot.Services
{
    public class SprintLifecycleService : ISprintLifecycleService
    {
        private readonly ISprintRepository _sprintRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<SprintRiskAlert> _sprintRiskAlertRepository;
        private readonly IRepository<UserStory> _userStoryRepository;
        private readonly IProjectEmployeeRepository _projectEmployeeRepository;
        private readonly ILogger<SprintLifecycleService> _logger;
        private readonly INotificationService _notificationService;
        private readonly ICalenderService _calenderService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly ILocalizationService _localizationService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IRepository<User> _userRepository;

        public SprintLifecycleService(
            ISprintRepository sprintRepository,
            IRepository<Project> projectRepository,
            IRepository<SprintRiskAlert> sprintRiskAlertRepository,
            ILogger<SprintLifecycleService> logger,
            INotificationService notificationService = null!,
            ICalenderService calenderService = null!,
            IRepository<UserStory> userStoryRepository = null!,
            IGoogleCalendarService googleCalendarService = null!,
            IProjectEmployeeRepository projectEmployeeRepository = null!,
            ILocalizationService localizationService = null!,
            IBackgroundJobClient backgroundJobClient = null!,
            IRepository<User> userRepository = null!)
        {
            _sprintRepository = sprintRepository;
            _projectRepository = projectRepository;
            _sprintRiskAlertRepository = sprintRiskAlertRepository;
            _userStoryRepository = userStoryRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _logger = logger;
            _notificationService = notificationService;
            _calenderService = calenderService;
            _googleCalendarService = googleCalendarService;
            _localizationService = localizationService;
            _backgroundJobClient = backgroundJobClient;
            _userRepository = userRepository;
        }

        public async Task<Result<PagedResult<SprintListItemDto>>> GetAllSprintsPagedAsync(Guid projectId, Guid userId, int page, int pageSize, string? statusFilter, string? dateFrom, string? dateTo, string lang = "en", CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty) return Result.Failure<PagedResult<SprintListItemDto>>(SprintErrors.InvalidProject);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<PagedResult<SprintListItemDto>>(SprintErrors.ProjectNotFound);
            }

            if (project.ManagerId != userId)
            {
                return Result.Failure<PagedResult<SprintListItemDto>>(TaskPilot.Models.Common.Errors.CommonErrors.Forbidden());
            }

            var query = _sprintRepository.GetQueryable()
                .Where(s => s.ProjectId == projectId && !s.IsDeleted);

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<SprintStatus>(statusFilter, true, out var parsedStatus))
                {
                    query = query.Where(s => s.Status == parsedStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var fromDt))
            {
                query = query.Where(s => s.StartDate >= fromDt);
            }

            if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var toDt))
            {
                query = query.Where(s => s.EndDate <= toDt);
            }

            var totalCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);

            var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(query
                .OrderByDescending(s => s.StartDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SprintListItemDto
                {
                    SprintId = s.Id,
                    Title = lang == "ar" && !string.IsNullOrEmpty(s.TitleAr) ? s.TitleAr : s.TitleEn,
                    SprintGoal = lang == "ar" && !string.IsNullOrEmpty(s.SprintGoalAr) ? s.SprintGoalAr : s.SprintGoalEn,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = s.Status.ToString(),
                    UserStoriesCount = s.UserStories.Count,
                    TasksCount = s.Tasks.Count
                }), cancellationToken);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Result.Success(new PagedResult<SprintListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            });
        }

        public async Task<Result<SprintStatusDto>> StartSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty) return Result.Failure<SprintStatusDto>(SprintErrors.InvalidProject);
            if (sprintId == Guid.Empty) return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprint);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.ProjectNotFound);
            }

            if (_projectEmployeeRepository != null)
            {
                var assignedEmployees = await _projectEmployeeRepository.GetEmployeeIdsByProjectAsync(projectId, cancellationToken);
                if (!assignedEmployees.Any())
                {
                    return Result.Failure<SprintStatusDto>(SprintErrors.NoEmployeesAssigned);
                }
            }

            var sprint = await _sprintRepository.GetSprintWithTasksAsync(sprintId);
            //var sprint = await _sprintRepository.GetByIdAsync(sprintId);
            if (sprint == null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotFound);
            }

            if (sprint.ProjectId != projectId)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintDoesNotBelongToProject);
            }

            if (sprint.Tasks.Any(t => t.EmployeeId == null || t.EmployeeId == Guid.Empty))
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.UnassignedTasksExist);
            }

            if (sprint.Status == SprintStatus.Active)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyActive);
            }

            if (sprint.Status == SprintStatus.Completed)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyCompleted);
            }

            if (sprint.Status != SprintStatus.Planned)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprintStatus);
            }

            var activeSprint = await _sprintRepository.GetActiveSprintByProjectIdAsync(projectId, cancellationToken);
            if (activeSprint != null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.AnotherSprintAlreadyActive);
            }

            if (project.Status == ProjectStatus.Draft)
            {
                project.Status = ProjectStatus.Active;
            }

            var originalDuration = sprint.EndDate - sprint.StartDate;
            sprint.Status = SprintStatus.Active;
            sprint.StartDate = DateTime.UtcNow;
            sprint.EndDate = sprint.StartDate + originalDuration;

            if (_backgroundJobClient != null)
            {
                _backgroundJobClient.Schedule<SprintCompletionJob>(
                    job => job.ExecuteAsync(sprint.Id),
                    new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDate, DateTimeKind.Utc)));

                _backgroundJobClient.Schedule<SprintEndReminderJob>(
                    job => job.ExecuteAsync(sprint.Id),
                    new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDate.AddDays(-1), DateTimeKind.Utc)));
            }

            // --- add Google calendar to PM as sprint event ---
            try
            {
                if (_googleCalendarService != null && sprint.Project?.ManagerId != null)
                {
                    await _googleCalendarService.AddEventToCalendarAsync(
                        sprint.Project.ManagerId,
                        $"Sprint beginning: {sprint.TitleEn}",
                        $"Sprint goals: {sprint.SprintGoalEn}",
                        sprint.StartDate,
                        sprint.EndDate
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"event didn't get added: {ex.Message}");
            }

            //addd to calendar
            foreach (var task in sprint.Tasks)
            {
                await _calenderService.GenerateEventsForAssignedTaskAsync(task, task.EmployeeId.Value, DateTime.UtcNow);
                try
                {
                    if (_googleCalendarService != null && task.EmployeeId.HasValue)
                    {
                        var startTime = DateTime.UtcNow;
                        var endTime = startTime.AddHours((double)task.EstimatedHours);

                        await _googleCalendarService.AddEventToCalendarAsync(
                            task.EmployeeId.Value,
                            $"Task: {task.TitleEn}",
                            task.DescriptionEn ?? "No description available",
                            startTime,
                            endTime
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"event didn't get added: {ex.Message}");
                }
                await _notificationService.SendAsync(
               userId: task.EmployeeId.Value,
               type: NotificationType.TaskAssigned,
               messageEn: $"You have been assigned to task '{task.TitleEn}'.",
               messageAr: $"تم تكليفك بمهمة '{task.TitleAr ?? task.TitleEn}'.",
               url: $"/projects/{projectId}/board/tasks/{task.Id}"
           );

            }
            _logger.LogInformation("Sprint {SprintId} started for Project {ProjectId}", sprintId, projectId);

            return Result.Success(new SprintStatusDto
            {
                SprintId = sprint.Id,
                Status = sprint.Status.ToString()
            });
        }
        public async Task<Result<SprintStatusDto>> CancelSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null) return Result.Failure<SprintStatusDto>(SprintErrors.ProjectNotFound);

            var sprint = await _sprintRepository.GetQueryable()
                .Include(s => s.Tasks)
                .Include(s => s.UserStories)
                .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId, cancellationToken);
                
            if (sprint == null) return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotFound);

            if (sprint.Status == SprintStatus.Active)
                return Result.Failure<SprintStatusDto>(SprintErrors.CannotCancelActiveSprint);
                
            if (sprint.Status == SprintStatus.Cancelled)
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyCancelled);
                
            if (sprint.Status == SprintStatus.Completed)
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyCompleted);

            sprint.Status = SprintStatus.Cancelled;

            // Return Tasks to Backlog
            foreach (var task in sprint.Tasks)
            {
                task.SprintId = null;
                task.EmployeeId = null;
                task.Status = TaskItemStatus.ToDo; // Reset status just in case
            }

            // Return UserStories to Backlog
            foreach (var story in sprint.UserStories)
            {
                story.SprintId = null;
            }

            return Result.Success(new SprintStatusDto { SprintId = sprint.Id, Status = sprint.Status.ToString() });
        }

        public async Task<Result<SprintStatusDto>> CompleteSprintAsync(
    Guid projectId,
    Guid sprintId,
    ReviewTaskAction? reviewAction = null,
    CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<SprintStatusDto>(SprintErrors.InvalidProject);

            if (sprintId == Guid.Empty)
                return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprint);

            var sprint = await _sprintRepository.GetSprintWithTasksAsync(sprintId, cancellationToken);

            if (sprint == null)
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotFound);

            if (sprint.ProjectId != projectId)
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintDoesNotBelongToProject);

            if (sprint.Status == SprintStatus.Completed)
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyCompleted);

            if (sprint.Status == SprintStatus.Planned)
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotStarted);

            if (sprint.Status != SprintStatus.Active)
                return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprintStatus);

            var reviewTasks = sprint.Tasks.Where(t => t.Status == TaskItemStatus.Review).ToList();
            if (reviewTasks.Any() && reviewAction == null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.HasUnfinishedTasks);
            }

            if (reviewAction == ReviewTaskAction.AcceptAll)
            {
                foreach (var task in reviewTasks)
                {
                    task.Status = TaskItemStatus.Done;
                }
            }
            sprint.Status = SprintStatus.Completed;
            sprint.EndDate = DateTime.UtcNow;


            var unfinishedTasks = sprint.Tasks.Where(t => t.Status != TaskItemStatus.Done).ToList();
            if (unfinishedTasks.Any() && _userStoryRepository != null)
            {
                var storyIdsWithUnfinishedTasks = unfinishedTasks
                    .Where(t => t.UserStoryId.HasValue)
                    .Select(t => t.UserStoryId!.Value)
                    .Distinct()
                    .ToList();

                var storiesToUpdate = await _userStoryRepository.FindAsync(s => s.SprintId == sprintId && storyIdsWithUnfinishedTasks.Contains(s.Id));
                foreach (var story in storiesToUpdate)
                {
                    story.SprintId = null;
                }
            }

            foreach (var task in sprint.Tasks.ToList())
            {
                if (task.Status != TaskItemStatus.Done)
                {
                    task.Status = TaskItemStatus.ToDo;

                    var alert = new SprintRiskAlert
                    {
                        SprintId = sprint.Id,
                        RiskType = SprintRiskType.UnfinishedTask,
                        Severity = RiskSeverity.High,
                        AffectedTaskId = task.Id,
                        AffectedEmployeeId = task.EmployeeId,
                        MessageEn = $"Task '{task.TitleEn}' was not completed during the sprint.",
                        MessageAr = $"المهمة '{task.TitleAr}' لم تكتمل خلال السبرنت.",
                        LastDetectedAt = DateTime.UtcNow,
                        IsDismissed = false
                    };
                    await _sprintRiskAlertRepository.AddAsync(alert);

                    task.SprintId = null;
                    task.EmployeeId = null;
                }
            }

            _logger.LogInformation(
                "Sprint {SprintId} completed for Project {ProjectId}",
                sprintId,
                projectId);

            return Result.Success(new SprintStatusDto
            {
                SprintId = sprint.Id,
                Status = sprint.Status.ToString()
            });
        }
        public async Task<bool> EnsureCompletedIfDueAsync(
    Guid sprintId,
    CancellationToken cancellationToken = default)
        {
            var sprint = await _sprintRepository.GetSprintWithTasksAsync(
                sprintId,
                cancellationToken);

            if (sprint == null)
                return false;

            if (sprint.IsDeleted)
                return false;

            if (sprint.Status == SprintStatus.Cancelled)
                return false;

            if (sprint.Status == SprintStatus.Completed)
                return true;

            if (sprint.EndDate > DateTime.UtcNow)
                return false;

            sprint.Status = SprintStatus.Completed;

            var unfinishedTasks = sprint.Tasks.Where(t => t.Status != TaskItemStatus.Done).ToList();
            if (unfinishedTasks.Any() && _userStoryRepository != null)
            {
                var storyIdsWithUnfinishedTasks = unfinishedTasks
                    .Where(t => t.UserStoryId.HasValue)
                    .Select(t => t.UserStoryId!.Value)
                    .Distinct()
                    .ToList();

                var storiesToUpdate = await _userStoryRepository.FindAsync(s => s.SprintId == sprintId && storyIdsWithUnfinishedTasks.Contains(s.Id));
                foreach (var story in storiesToUpdate)
                {
                    story.SprintId = null;
                }
            }

            foreach (var task in sprint.Tasks.ToList())
            {
                if (task.Status != TaskItemStatus.Done)
                {
                    task.Status = TaskItemStatus.ToDo;

                    var alert = new SprintRiskAlert
                    {
                        SprintId = sprint.Id,
                        RiskType = SprintRiskType.UnfinishedTask,
                        Severity = RiskSeverity.High,
                        AffectedTaskId = task.Id,
                        AffectedEmployeeId = task.EmployeeId,
                        MessageEn = $"Task '{task.TitleEn}' was not completed during the sprint.",
                        MessageAr = $"المهمة '{task.TitleAr}' لم تكتمل خلال السبرنت.",
                        LastDetectedAt = DateTime.UtcNow,
                        IsDismissed = false
                    };
                    await _sprintRiskAlertRepository.AddAsync(alert);

                    task.SprintId = null;
                    task.EmployeeId = null;
                }
            }

            _logger.LogInformation(
                "Sprint {SprintId} auto completed.",
                sprint.Id);

            return true;
        }
        
        public async Task<Result<ActiveSprintDto>> GetActiveSprintAsync(
    Guid projectId,
    CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<ActiveSprintDto>(SprintErrors.InvalidProject);

            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project == null)
                return Result.Failure<ActiveSprintDto>(SprintErrors.ProjectNotFound);

            var activeSprint =
                await _sprintRepository.GetActiveSprintByProjectIdAsync(
                    projectId,
                    cancellationToken);

            if (activeSprint == null)
                return Result.Failure<ActiveSprintDto>(SprintErrors.SprintNotFound);

            var sprintWithTasks =
                await _sprintRepository.GetSprintWithTasksAsync(
                    activeSprint.Id,
                    cancellationToken);

            if (sprintWithTasks == null)
                return Result.Failure<ActiveSprintDto>(SprintErrors.SprintNotFound);

            var totalTasks = sprintWithTasks.Tasks.Count;

            double completionPercentage = 0;

            if (totalTasks > 0)
            {
                var doneTasks =
                    sprintWithTasks.Tasks.Count(x => x.Status == TaskItemStatus.Done);

                completionPercentage =
                    Math.Round((double)doneTasks / totalTasks * 100, 2);
            }

            var daysRemaining =
                Math.Max(
                    0,
                    (activeSprint.EndDate.Date - DateTime.UtcNow.Date).Days);

            return Result.Success(new ActiveSprintDto
            {
                SprintId = activeSprint.Id,
                TitleEn = activeSprint.TitleEn,
                TitleAr = activeSprint.TitleAr,
                StartDate = activeSprint.StartDate,
                EndDate = activeSprint.EndDate,
                DaysRemaining = daysRemaining,
                CompletionPercentage = completionPercentage
            });
        }

        public async Task<Result<ActiveSprintDto>> GetPlannedSprintAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<ActiveSprintDto>(SprintErrors.InvalidProject);

            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project == null)
                return Result.Failure<ActiveSprintDto>(SprintErrors.ProjectNotFound);

            var plannedSprint =
                await _sprintRepository.GetPlannedSprintByProjectIdAsync(
                    projectId,
                    cancellationToken);

            if (plannedSprint == null)
                return Result.Failure<ActiveSprintDto>(SprintErrors.SprintNotFound);

            var sprintWithTasks =
                await _sprintRepository.GetSprintWithTasksAsync(
                    plannedSprint.Id,
                    cancellationToken);

            if (sprintWithTasks == null)
                return Result.Failure<ActiveSprintDto>(SprintErrors.SprintNotFound);

            var totalTasks = sprintWithTasks.Tasks.Count;

            double completionPercentage = 0;

            if (totalTasks > 0)
            {
                var doneTasks =
                    sprintWithTasks.Tasks.Count(x => x.Status == TaskItemStatus.Done);

                completionPercentage =
                    Math.Round((double)doneTasks / totalTasks * 100, 2);
            }

            var daysRemaining =
                Math.Max(
                    0,
                    (plannedSprint.EndDate.Date - DateTime.UtcNow.Date).Days);

            return Result.Success(new ActiveSprintDto
            {
                SprintId = plannedSprint.Id,
                TitleEn = plannedSprint.TitleEn,
                TitleAr = plannedSprint.TitleAr,
                StartDate = plannedSprint.StartDate,
                EndDate = plannedSprint.EndDate,
                DaysRemaining = daysRemaining,
                CompletionPercentage = completionPercentage
            });
        }
        public async Task<Result<LatestCompletedSprintDto>> GetLatestCompletedSprintAsync(Guid projectId)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<LatestCompletedSprintDto>(SprintErrors.InvalidProject);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return Result.Failure<LatestCompletedSprintDto>(SprintErrors.ProjectNotFound);

            var completedSprint = _sprintRepository.GetQueryable()
                .Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Completed && !s.IsDeleted)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();

            if (completedSprint == null)
                return Result.Failure<LatestCompletedSprintDto>(SprintErrors.SprintNotFound);

            return Result.Success(new LatestCompletedSprintDto
            {
                SprintId = completedSprint.Id,
                TitleEn = completedSprint.TitleEn,
                TitleAr = completedSprint.TitleAr,
                EndDate = completedSprint.EndDate
            });
        }

        public async Task<Result<IEnumerable<SprintBoardTaskDto>>> GetSprintTasksAsync(
            Guid projectId,
            Guid sprintId,
            CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty) return Result.Failure<IEnumerable<SprintBoardTaskDto>>(SprintErrors.InvalidProject);
            if (sprintId == Guid.Empty) return Result.Failure<IEnumerable<SprintBoardTaskDto>>(SprintErrors.InvalidSprint);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<IEnumerable<SprintBoardTaskDto>>(SprintErrors.ProjectNotFound);
            }

            var sprint = await _sprintRepository.GetSprintWithTasksAsync(sprintId, cancellationToken);
            if (sprint == null)
            {
                return Result.Failure<IEnumerable<SprintBoardTaskDto>>(SprintErrors.SprintNotFound);
            }

            if (sprint.ProjectId != projectId)
            {
                return Result.Failure<IEnumerable<SprintBoardTaskDto>>(SprintErrors.SprintDoesNotBelongToProject);
            }
            bool isArabic = _localizationService?.CurrentLanguage == "ar";
            var tasks = sprint.Tasks.Select(t => new SprintBoardTaskDto
            {
                TaskId = t.Id,
                UserStoryId = t.UserStoryId ?? Guid.Empty,
                TitleEn = t.TitleEn,
                TitleAr = t.TitleAr,
                DescriptionEn = t.DescriptionEn,
                DescriptionAr = t.DescriptionAr,
                AcceptanceCriteriaEn = t.AcceptanceCriteriaEn,
                AcceptanceCriteriaAr = t.AcceptanceCriteriaAr,
                EstimatedHours = t.EstimatedHours,
                ActualHours = t.ActualHours,
                Type = t.Type.ToString(),
                Priority = t.Priority.ToString(),
                Status = t.Status.ToString(),
                AssigneeId = t.EmployeeId,
                AssigneeName = t.Employee != null ? (isArabic ? $"{t.Employee.FirstNameAr} {t.Employee.LastNameAr}" : $"{t.Employee.FirstNameEn} {t.Employee.LastNameEn}") : null
            }).ToList();

            return Result.Success<IEnumerable<SprintBoardTaskDto>>(tasks);
        }
    }
}

