using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskPilot.Data.Context;
using TaskPilot.DTOs.Calender;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class CalenderService : ICalenderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILocalizationService _localizationService;

        private static readonly TimeSpan WorkDayStart = new TimeSpan(9, 0, 0);
        private const int WorkMinutesPerDay = 480;

        public CalenderService(ApplicationDbContext context, ILocalizationService localizationService)
        {
            _context = context;
            _localizationService = localizationService;
        }
        public async Task<Result> GenerateEventsForAssignedTaskAsync(TaskItem task, Guid employeeId, DateTime startDate)// start date is datetime.now of the assignment
        {
            var companyConfigs = await _context.TaskItems
                .Include(t => t.Sprint)
                .ThenInclude(s => s.Project)
                .ThenInclude(p => p.Company)
                .Where(t => t.Id == task.Id)
                .Select(t => new { t.Sprint.Project.Company.WorkingDaysMask, t.Sprint.Project.Company.WorkingHoursPerDay, t.Sprint.ProjectId })
                .FirstOrDefaultAsync();

            int workingDaysMask = 62;
            decimal hoursPerDay = 8.0m;
            decimal allocationPercentage = 100m;

            if (companyConfigs != null)
            {
                workingDaysMask = companyConfigs.WorkingDaysMask;
                hoursPerDay = companyConfigs.WorkingHoursPerDay;

                var projectEmployee = await _context.ProjectEmployees
                    .FirstOrDefaultAsync(pe => pe.ProjectId == companyConfigs.ProjectId && pe.EmployeeId == employeeId);
                
                if (projectEmployee != null)
                {
                    allocationPercentage = projectEmployee.AllocationPercentage;
                }
            }

            double maxMinutesPerDay = (double)hoursPerDay * (double)(allocationPercentage / 100m) * 60;
            if (maxMinutesPerDay <= 0) maxMinutesPerDay = 480; // fallback just in case

            double remainingMinutes = (double)task.EstimatedHours * 60;
            DateTime currentDay = startDate.Date;

            // if task is assigned on weekend 
            while (!IsWorkingDay(currentDay.DayOfWeek, workingDaysMask))
            {
                currentDay = currentDay.AddDays(1);
            }

            while (remainingMinutes > 0)
            {
                DateTime workStart = currentDay.Add(WorkDayStart);

                // Schedule either the remaining minutes or the full work day
                double minutesToSchedule = Math.Min(remainingMinutes, maxMinutesPerDay);
                DateTime slotEnd = workStart.AddMinutes(minutesToSchedule);

                await CreateEventRecord(task, employeeId, workStart, slotEnd);

                remainingMinutes -= minutesToSchedule;

                currentDay = currentDay.AddDays(1);

                while (!IsWorkingDay(currentDay.DayOfWeek, workingDaysMask))
                {
                    currentDay = currentDay.AddDays(1);
                }
            }

            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result> StageEventsForAssignedTasksAsync(
            Guid projectId,
            IReadOnlyCollection<TaskItem> tasks,
            DateTime startDate,
            CancellationToken cancellationToken = default)
        {
            if (tasks.Count == 0)
            {
                return Result.Success();
            }

            var companyConfig = await _context.Projects
                .Where(project => project.Id == projectId)
                .Select(project => new
                {
                    project.Company.WorkingDaysMask,
                    project.Company.WorkingHoursPerDay
                })
                .FirstOrDefaultAsync(cancellationToken);

            var employeeIds = tasks
                .Where(task => task.EmployeeId.HasValue)
                .Select(task => task.EmployeeId!.Value)
                .Distinct()
                .ToList();

            var allocations = await _context.ProjectEmployees
                .Where(projectEmployee =>
                    projectEmployee.ProjectId == projectId &&
                    employeeIds.Contains(projectEmployee.EmployeeId))
                .ToDictionaryAsync(
                    projectEmployee => projectEmployee.EmployeeId,
                    projectEmployee => projectEmployee.AllocationPercentage,
                    cancellationToken);

            var workingDaysMask = companyConfig?.WorkingDaysMask ?? 62;
            var hoursPerDay = companyConfig?.WorkingHoursPerDay ?? 8.0m;
            var events = new List<CalenderEvent>();

            foreach (var task in tasks)
            {
                if (!task.EmployeeId.HasValue)
                {
                    continue;
                }

                var employeeId = task.EmployeeId.Value;
                var allocationPercentage = allocations.GetValueOrDefault(employeeId, 100m);
                var maxMinutesPerDay = (double)hoursPerDay * (double)(allocationPercentage / 100m) * 60;
                if (maxMinutesPerDay <= 0)
                {
                    maxMinutesPerDay = WorkMinutesPerDay;
                }

                var remainingMinutes = (double)task.EstimatedHours * 60;
                var currentDay = startDate.Date;

                while (!IsWorkingDay(currentDay.DayOfWeek, workingDaysMask))
                {
                    currentDay = currentDay.AddDays(1);
                }

                while (remainingMinutes > 0)
                {
                    var workStart = currentDay.Add(WorkDayStart);
                    var minutesToSchedule = Math.Min(remainingMinutes, maxMinutesPerDay);

                    events.Add(new CalenderEvent
                    {
                        Id = Guid.NewGuid(),
                        EmployeeId = employeeId,
                        Title = task.TitleEn,
                        StartDate = workStart,
                        EndDate = workStart.AddMinutes(minutesToSchedule),
                        Description = _localizationService.CurrentLanguage == "en"
                            ? task.DescriptionEn
                            : task.DescriptionAr,
                        Type = CalenderEventType.AssignedTask,
                        TaskPriority = task.Priority,
                        Status = TaskItemStatus.ToDo,
                        RelatedTaskId = task.Id
                    });

                    remainingMinutes -= minutesToSchedule;
                    currentDay = currentDay.AddDays(1);

                    while (!IsWorkingDay(currentDay.DayOfWeek, workingDaysMask))
                    {
                        currentDay = currentDay.AddDays(1);
                    }
                }
            }

            await _context.CalenderEvents.AddRangeAsync(events, cancellationToken);
            return Result.Success();
        }

        private bool IsWorkingDay(DayOfWeek day, int mask)
        {
            return (mask & (1 << (int)day)) != 0;
        }

        private async Task CreateEventRecord(TaskItem task, Guid employeeId, DateTime start, DateTime end)
        {
            string lang = _localizationService.CurrentLanguage;
            var newEvent = new CalenderEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                Title = task.TitleEn,
                StartDate = start,
                EndDate = end,
                Description = lang == "en" ? task.DescriptionEn : task.DescriptionAr,
                Type = CalenderEventType.AssignedTask,
                TaskPriority = task.Priority,
                Status = TaskItemStatus.ToDo,
                RelatedTaskId = task.Id
            };
            await _context.CalenderEvents.AddAsync(newEvent);
        }





        public async Task<Result<CalendarDashboardResponseDto>> GetCalendarDashboardAsync(Guid employeeId, DateOnly start, DateOnly end)
        {
            DateTime startDateTime = start.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = end.AddDays(1).ToDateTime(TimeOnly.MinValue);

            // 1. استخدام Select قبل ToListAsync لترجمة الكود إلى استعلام SQL آمن وسريع
            var eventDtos = await _context.CalenderEvents
                .Where(ce => ce.EmployeeId == employeeId &&
                             ce.StartDate < endDateTime &&
                             ce.EndDate >= startDateTime)
                .OrderBy(e => e.StartDate)
                .Select(e => new CalendarBlockDto
                {
                    Id = e.Id,
                    Description = e.Description,
                    EventType = e.Type.ToString(),
                    Title = e.Title ?? string.Empty,
                    Priority = e.TaskPriority.ToString(),
                    // الشرط هنا آمن تماماً لأن التنفيذ يحدث داخل محرك الداتابيز (SQL)
                    Status = e.Type == CalenderEventType.AssignedTask
                        ? (e.RelatedTask != null ? e.RelatedTask.Status.ToString() : e.Status.ToString())
                        : e.Status.ToString(),
                    RelatedTaskId = e.RelatedTaskId,
                    Start = e.StartDate,
                    End = e.EndDate,
                })
                .ToListAsync(); // <-- التنفيذ وجلب البيانات يحدث هنا في النهاية

            // 2. العمليات الحسابية
            var today = DateTime.Today;
            var todayEvents = eventDtos.Where(e => e.Start.Date == today).ToList();
            int scheduledTodayMinutes = (int)todayEvents.Sum(e => (e.End - e.Start).TotalMinutes);

            string workloadStatus;
            if (scheduledTodayMinutes <= WorkMinutesPerDay * 0.75)
                workloadStatus = WorkloadStatus.Balanced.ToString();
            else if (scheduledTodayMinutes <= WorkMinutesPerDay)
                workloadStatus = WorkloadStatus.Busy.ToString();
            else
                workloadStatus = WorkloadStatus.Overloaded.ToString();

            var response = new CalendarDashboardResponseDto
            {
                Events = eventDtos,
                WorkingHours = WorkMinutesPerDay / 60,
                ScheduledHours = scheduledTodayMinutes / 60,
                FreeSlots = Math.Max(0, WorkMinutesPerDay - scheduledTodayMinutes),
                WorkloadStatus = workloadStatus,
                AiSuggestions = new List<string>() // to be implemented 
            };

            return Result.Success(response);
        }


        public async Task<Result<CalenderTaskDetailsDto>> GetCalenderEventDetailsAsync(Guid eventId, Guid employeeId)
        {
            var taskDetails = await _context.CalenderEvents
                .Where(cee => cee.Id == eventId && employeeId == cee.EmployeeId)
                .Select(cee => new
                {
                    EventId = cee.Id,
                    Title = cee.Title,
                    Description = cee.Description,
                    Start = cee.StartDate,
                    End = cee.EndDate,
                    IsAssigned = cee.RelatedTaskId != null,
                    EventType = cee.Type.ToString(),
                    Priority = cee.TaskPriority.ToString(),
                    Status = cee.Type == CalenderEventType.AssignedTask ? (cee.RelatedTask != null ? cee.RelatedTask.Status.ToString() : cee.Status.ToString()) : cee.Status.ToString(),
                    RelatedTaskId = cee.RelatedTaskId,
                    //Status = cee.Status.ToString(),
                    ProjectName = cee.RelatedTask != null ? cee.RelatedTask.UserStory.Sprint.TitleEn : null,
                    SprintTitle = cee.RelatedTask != null ? cee.RelatedTask.Sprint.TitleEn : null
                })
                .FirstOrDefaultAsync();

            if (taskDetails == null)
            {
                return Result.Failure<CalenderTaskDetailsDto>(CalenderErrors.EventNotFoundOrUnauthorized);
            }

            var dto = new CalenderTaskDetailsDto
            {
                Id = taskDetails.EventId,
                Title = taskDetails.Title,
                Description = taskDetails.Description,
                TaskType = taskDetails.EventType,
                Priority = taskDetails.Priority,
                StartDate = taskDetails.Start,
                EndDate = taskDetails.End,
                Status = taskDetails.Status,
                DurationInMinutes = (int)(taskDetails.End - taskDetails.Start).TotalMinutes
            };

            if (taskDetails.IsAssigned)
            {

                dto.RelatedTaskId = taskDetails.RelatedTaskId.Value;
                dto.RelatedSprint = taskDetails.SprintTitle;
                dto.ProjectName = taskDetails.ProjectName;
                dto.AiQuickSummary = null; // to be implemented (context summary)
            }

            return Result.Success(dto);
        }



        public async Task<Result<CalendarBlockDto>> CreatePersonalEventAsync(Guid employeeId, CreateCalendarEventDto dto)
        {
            var endDate = dto.StartDate.AddMinutes(dto.DurationInMinutes);

            var newEvent = new CalenderEvent
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                Title = dto.Title,
                Description = dto.Description,
                StartDate = dto.StartDate,
                EndDate = endDate,
                Type = dto.EventType,
                TaskPriority = dto.Priority,
                Status = TaskItemStatus.ToDo,
            };

            await _context.CalenderEvents.AddAsync(newEvent);
            var result = new CalendarBlockDto
            {
                Id = newEvent.Id,
                Title = newEvent.Title,
                Description = newEvent.Description,
                Start = newEvent.StartDate,
                End = newEvent.EndDate,
                EventType = newEvent.Type.ToString(),
                Status = newEvent.Status.ToString(),
                Priority = newEvent.TaskPriority.ToString()
            };
            return Result.Success(result);
        }

        public async Task<Result> RescheduleEventAsync(Guid eventId, Guid employeeId, RescheduleEventDto dto)
        {
            var existingEvent = await _context.CalenderEvents
                .FirstOrDefaultAsync(e => e.Id == eventId && e.EmployeeId == employeeId);

            if (existingEvent is null)
            {
                return Result.Failure(CalenderErrors.EventNotFoundOrUnauthorized);
            }
            // cant reschedule if assigned task
            if (existingEvent.Type == CalenderEventType.AssignedTask)
            {
                return Result.Failure(CalenderErrors.CannotRescheduleAssignedTask);
            }

            existingEvent.StartDate = dto.NewStart;
            existingEvent.EndDate = dto.NewEnd;

            return Result.Success();
        }

        public async Task<Result> UpdateEventAsync(Guid eventId, Guid employeeId, UpdateCalendarEventDto dto)
        {
            var existingEvent = await _context.CalenderEvents.Include(e => e.RelatedTask)
                .FirstOrDefaultAsync(e => e.Id == eventId && e.EmployeeId == employeeId);

            if (existingEvent is null)
            {
                return Result.Failure(CalenderErrors.EventNotFoundOrUnauthorized);
            }

            if (existingEvent.Type == CalenderEventType.AssignedTask)
            {
                // For assigned tasks, we might only allow updating the status
                if (dto.Status.HasValue)
                {
                    existingEvent.Status = dto.Status.Value;
                    existingEvent.RelatedTask.Status = dto.Status.Value;
                }
            }
            else
            {
                // For personal events, allow updating all provided fields
                if (dto.Title != null) existingEvent.Title = dto.Title;
                if (dto.Description != null) existingEvent.Description = dto.Description;
                if (dto.EventType.HasValue) existingEvent.Type = dto.EventType.Value;
                if (dto.Priority.HasValue) existingEvent.TaskPriority = dto.Priority.Value;
                if (dto.Status.HasValue) existingEvent.Status = dto.Status.Value;
                else return Result.Failure(CalenderErrors.CannotUpdateStatusOfAssignedTask);
                if (dto.StartDate.HasValue)
                {
                    var duration = existingEvent.EndDate - existingEvent.StartDate;
                    if (dto.DurationInMinutes.HasValue)
                    {
                        duration = TimeSpan.FromMinutes(dto.DurationInMinutes.Value);
                    }
                    existingEvent.StartDate = dto.StartDate.Value;
                    existingEvent.EndDate = dto.StartDate.Value.Add(duration);
                }
                else if (dto.DurationInMinutes.HasValue)
                {
                    existingEvent.EndDate = existingEvent.StartDate.AddMinutes(dto.DurationInMinutes.Value);
                }
                //if(existingEvent.Type == CalenderEventType.AssignedTask && existingEvent.RelatedTask != null)
                //{
                //   existingEvent.RelatedTask.Status = dto.Status.Value;
                //}
            }

            return Result.Success();
        }

        public async Task<Result> DeleteEventAsync(Guid eventId, Guid employeeId)
        {
            var existingEvent = await _context.CalenderEvents
                .FirstOrDefaultAsync(e => e.Id == eventId && e.EmployeeId == employeeId);

            if (existingEvent is null)
            {
                return Result.Failure(CalenderErrors.EventNotFoundOrUnauthorized);
            }

            if (existingEvent.Type == CalenderEventType.AssignedTask)
            {
                return Result.Failure(CalenderErrors.CannotDeleteAssignedTask);
            }

            _context.CalenderEvents.Remove(existingEvent);
            return Result.Success();
        }


        #region 5. Workload Analytics (Cleaned up)

        public async Task<Result<WorkloadResponseDto>> GetEmployeeWorkloadAsync(Guid employeeId)
        {
            var employee = await _context.Set<Employee>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee is null)
                return Result.Failure<WorkloadResponseDto>(new Error("Employee.NotFound"));

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayEvents = await _context.CalenderEvents
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.StartDate >= today && e.StartDate < tomorrow)
                .OrderBy(e => e.StartDate)
                .ToListAsync();

            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);

            var weekEvents = await _context.CalenderEvents
                .AsNoTracking()
                .Where(e => e.EmployeeId == employeeId && e.StartDate >= startOfWeek && e.StartDate < endOfWeek)
                .OrderBy(e => e.StartDate)
                .ToListAsync();

            var response = new WorkloadResponseDto
            {
                Summary = BuildSummary(todayEvents),
                Breakdown = BuildBreakdown(todayEvents),
                Timeline = BuildTimeline(todayEvents),
                WeekOverview = BuildWeekOverview(weekEvents, startOfWeek),
                QuickStats = BuildQuickStats(weekEvents),
                // SuggestedSlots= // to be implemented
            };

            return Result.Success(response);
        }

        private WorkloadSummaryDto BuildSummary(List<CalenderEvent> events)
        {
            var scheduledMinutes = (int)events.Sum(e => (e.EndDate - e.StartDate).TotalMinutes);
            var avaliableMinutes = Math.Max(0, WorkMinutesPerDay - scheduledMinutes);
            var overbookedMinutes = Math.Max(0, scheduledMinutes - WorkMinutesPerDay);

            string status;
            if (scheduledMinutes <= WorkMinutesPerDay * 0.75)
                status = WorkloadStatus.Balanced.ToString();
            else if (scheduledMinutes <= WorkMinutesPerDay)
                status = WorkloadStatus.Busy.ToString();
            else
                status = WorkloadStatus.Overloaded.ToString();

            return new WorkloadSummaryDto
            {
                Status = status,
                WorkingMinutes = WorkMinutesPerDay,
                ScheduledMinutes = scheduledMinutes,
                avaliableMinutes = avaliableMinutes,
                OverbookedMinutes = overbookedMinutes
            };
        }

        private WorkloadBreakdownDto BuildBreakdown(List<CalenderEvent> events)
        {
            int assigned = (int)events.Where(x => x.Type == CalenderEventType.AssignedTask).Sum(x => (x.EndDate - x.StartDate).TotalMinutes);
            int meetings = (int)events.Where(x => x.Type == CalenderEventType.Meeting).Sum(x => (x.EndDate - x.StartDate).TotalMinutes);
            int blockers = (int)events.Where(x => x.Type == CalenderEventType.Blocker).Sum(x => (x.EndDate - x.StartDate).TotalMinutes);
            int personal = (int)events.Where(x => x.Type == CalenderEventType.PersonalTask).Sum(x => (x.EndDate - x.StartDate).TotalMinutes);

            return new WorkloadBreakdownDto
            {
                AssignedMinutes = assigned,
                MeetingMinutes = meetings,
                PersonalMinutes = personal,
                BlockerMinutes = blockers,
                TotalMinutes = assigned + meetings + blockers + personal
            };
        }

        private List<TimelineEventDto> BuildTimeline(List<CalenderEvent> events)
        {
            return events.Select(x => new TimelineEventDto
            {
                Id = x.Id,
                Title = x.Title,
                Type = x.Type.ToString(),
                Start = x.StartDate,
                End = x.EndDate,
                DurationMinutes = (int)(x.EndDate - x.StartDate).TotalMinutes
            }).ToList();
        }

        private List<WeekOverviewDto> BuildWeekOverview(List<CalenderEvent> weekEvents, DateTime startOfWeek)
        {
            var result = new List<WeekOverviewDto>();

            for (int i = 0; i < 7; i++)
            {
                var day = startOfWeek.Date.AddDays(i);
                var nextDay = day.AddDays(1);

                var scheduled = (int)weekEvents
                    .Where(x => x.StartDate >= day && x.StartDate < nextDay)
                    .Sum(x => (x.EndDate - x.StartDate).TotalMinutes);

                result.Add(new WeekOverviewDto
                {
                    Date = DateOnly.FromDateTime(day),
                    ScheduledMinutes = scheduled,
                    CapacityMinutes = WorkMinutesPerDay,
                    IsOverloaded = scheduled > WorkMinutesPerDay
                });
            }

            return result;
        }

        private QuickStatsDto BuildQuickStats(List<CalenderEvent> weekEvents)
        {
            var totalScheduled = (int)weekEvents.Sum(x => (x.EndDate - x.StartDate).TotalMinutes);
            var meetingMinutes = (int)weekEvents.Where(x => x.Type == CalenderEventType.Meeting).Sum(x => (x.EndDate - x.StartDate).TotalMinutes);
            var averageDailyHours = Math.Round((totalScheduled / 7.0) / 60.0, 1);
            var freeMinutes = Math.Max(0, (WorkMinutesPerDay * 7) - totalScheduled);

            return new QuickStatsDto
            {
                AverageDailyLoadHours = averageDailyHours,
                FreeMinutesThisWeek = freeMinutes,
                MeetingMinutes = meetingMinutes
            };
        }

        #endregion
    }
}
