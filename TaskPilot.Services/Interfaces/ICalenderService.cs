using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.DTOs.Calender;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    public interface ICalenderService
    {
        Task<Result> GenerateEventsForAssignedTaskAsync(TaskItem task, Guid employeeId, DateTime startDate);
        Task<Result> StageEventsForAssignedTasksAsync(Guid projectId, IReadOnlyCollection<TaskItem> tasks, DateTime startDate, CancellationToken cancellationToken = default);
        Task<Result> RescheduleEventAsync(Guid eventId, Guid employeeId, RescheduleEventDto dto);
        Task<Result<CalendarBlockDto>> CreatePersonalEventAsync(Guid employeeId, CreateCalendarEventDto dto);
        Task<Result> UpdateEventAsync(Guid eventId, Guid employeeId, UpdateCalendarEventDto dto);
        Task<Result> DeleteEventAsync(Guid eventId, Guid employeeId);
        Task<Result<CalendarDashboardResponseDto>> GetCalendarDashboardAsync(Guid employeeId, DateOnly start, DateOnly end);
        Task<Result<CalenderTaskDetailsDto>> GetCalenderEventDetailsAsync(Guid eventId, Guid employeeId);
        Task<Result<WorkloadResponseDto>> GetEmployeeWorkloadAsync(Guid employeeId);
    }
}
