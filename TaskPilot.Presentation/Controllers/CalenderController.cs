using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Calender;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize]
    [Route("api/calendar")]
    public class CalenderController : ApiControllerBase
    {
        private readonly ICalenderService _calendarService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public CalenderController(
            ICalenderService calendarService,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser)
        {
            _calendarService = calendarService;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        [HttpGet("tasks")]
        public async Task<IActionResult> GetCalendarTasks(
            [FromQuery] DateOnly start,
            [FromQuery] DateOnly end)
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.GetCalendarDashboardAsync(
                _currentUser.UserId.Value,
                start,
                end);

            return HandleResult(result, SuccessCodes.Calendar.EventsRetrieved);
        }

        [HttpGet("tasks/{taskId:guid}")]
        public async Task<IActionResult> GetTaskDetails(Guid taskId)
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.GetCalenderEventDetailsAsync(
                taskId,
                _currentUser.UserId.Value);

            return HandleResult(result, SuccessCodes.Calendar.EventRetrieved);
        }

        [HttpPost("tasks")]
        public async Task<IActionResult> CreateTask(
            [FromBody] CreateCalendarEventDto request)
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.CreatePersonalEventAsync(
                _currentUser.UserId.Value,
                request);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            return HandleCreated(result, SuccessCodes.Calendar.EventCreated);
        }

        [HttpPatch("tasks/{taskId:guid}/reschedule")]
        public async Task<IActionResult> RescheduleTask(
            Guid taskId,
            [FromBody] RescheduleEventDto request)
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.RescheduleEventAsync(
                taskId,
                _currentUser.UserId.Value,
                request);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            return HandleResult(result, SuccessCodes.Calendar.EventRescheduled);
        }

        [HttpPatch("tasks/{taskId:guid}")]
        public async Task<IActionResult> UpdateTask(
            Guid taskId,
            [FromBody] UpdateCalendarEventDto request)
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.UpdateEventAsync(
                taskId,
                _currentUser.UserId.Value,
                request);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            return HandleResult(result, SuccessCodes.Calendar.EventUpdated);
        }

        [HttpDelete("tasks/{taskId:guid}")]
        public async Task<IActionResult> DeleteTask(Guid taskId)
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.DeleteEventAsync(
                taskId,
                _currentUser.UserId.Value);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            return HandleResult(result, SuccessCodes.Calendar.EventDeleted);
        }

        [HttpGet("workload")]
        public async Task<IActionResult> GetWorkload()
        {
            if (_currentUser.UserId is null)
                return Unauthorized();

            var result = await _calendarService.GetEmployeeWorkloadAsync(
                _currentUser.UserId.Value);

            return HandleResult(result, SuccessCodes.Calendar.WorkloadRetrieved);
        }
    }
}