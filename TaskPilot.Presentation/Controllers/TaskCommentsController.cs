using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Tasks.Comments;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class TaskCommentsController : ApiControllerBase
    {
        private readonly ITaskCommentService _commentService;
        private readonly IUnitOfWork _unitOfWork;

        public TaskCommentsController(
            ITaskCommentService commentService,
            IUnitOfWork unitOfWork)
        {
            _commentService = commentService;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("tasks/{taskId:guid}/comments")]
        public async Task<IActionResult> AddComment(
            Guid taskId,
            [FromBody] AddTaskCommentRequest request,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _commentService.AddCommentAsync(taskId, userId, request, ct);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(ct);
                return HandleCreated(result, SuccessCodes.Task.CommentAdded);
            }

            return HandleResult(result);
        }

        [HttpPut("tasks/comments/{commentId:guid}")]
        public async Task<IActionResult> UpdateComment(
            Guid commentId,
            [FromBody] UpdateTaskCommentRequest request,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _commentService.UpdateCommentAsync(commentId, userId, request, ct);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return HandleResult(result, SuccessCodes.Task.CommentUpdated);
        }

        [HttpDelete("tasks/comments/{commentId:guid}")]
        public async Task<IActionResult> DeleteComment(
            Guid commentId,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _commentService.DeleteCommentAsync(commentId, userId, ct);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return HandleResult(result, SuccessCodes.Task.CommentDeleted);
        }

        [HttpGet("tasks/{taskId:guid}/comments")]
        public async Task<IActionResult> GetComments(
            Guid taskId,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _commentService.GetCommentsAsync(taskId, userId, ct);

            return HandleResult(result, SuccessCodes.Task.CommentsRetrieved);
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
