using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Tasks;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class TasksController : ApiControllerBase
    {
        private readonly ITaskStatusService _taskStatusService;
        private readonly IUnitOfWork _unitOfWork;

        public TasksController(
            ITaskStatusService taskStatusService,
            IUnitOfWork unitOfWork)
        {
            _taskStatusService = taskStatusService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("projects/{projectId:guid}/tasks/my-tasks")]
        public async Task<IActionResult> GetMyTasks(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var result = await _taskStatusService.GetMyTasksAsync(projectId, userId, cancellationToken);

            return HandleResult(result, SuccessCodes.Task.MyTasksRetrieved);
        }

        [HttpPatch("tasks/{taskId:guid}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid taskId,
            [FromBody] UpdateTaskStatusRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var result = await _taskStatusService.UpdateStatusAsync(taskId, userId, request, cancellationToken);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return HandleResult(result, SuccessCodes.Task.StatusUpdated);
        }

        [HttpPatch("tasks/{taskId:guid}/actual-hours")]
        public async Task<IActionResult> LogActualHours(
            Guid taskId,
            [FromBody] LogActualHoursRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var result = await _taskStatusService.LogActualHoursAsync(taskId, userId, request, cancellationToken);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return HandleResult(result, SuccessCodes.Task.ActualHoursUpdated);
        }

        [HttpGet("projects/{projectId:guid}/sprints/{sprintId:guid}/tasks/my-tasks")]
        public async Task<IActionResult> GetMySprintTasks(
            Guid projectId,
            Guid sprintId,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var result = await _taskStatusService.GetMySprintTasksAsync(projectId, sprintId, userId, cancellationToken);
            return HandleResult(result, SuccessCodes.Task.MyTasksRetrieved);
        }

        [HttpPost("tasks/{taskId:guid}/reject-review")]
        public async Task<IActionResult> RejectReview(
            Guid taskId,
            [FromBody] PmRejectReviewRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var result = await _taskStatusService.PmRejectReviewAsync(taskId, userId, request, cancellationToken);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return HandleResult(result, SuccessCodes.Task.TaskRejected);
        }

        [HttpPost("tasks/{taskId:guid}/reopen")]
        public async Task<IActionResult> ReopenTask(
            Guid taskId,
            [FromBody] PmReopenTaskRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var result = await _taskStatusService.PmReopenTaskAsync(taskId, userId, request, cancellationToken);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return HandleResult(result, SuccessCodes.Task.TaskReopened);
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }
            return Guid.Empty;
        }
    }
}
