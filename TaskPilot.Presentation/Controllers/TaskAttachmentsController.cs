using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class TaskAttachmentsController : ApiControllerBase
    {
        private readonly ITaskAttachmentService _attachmentService;
        private readonly IUnitOfWork _unitOfWork;

        public TaskAttachmentsController(
            ITaskAttachmentService attachmentService,
            IUnitOfWork unitOfWork)
        {
            _attachmentService = attachmentService;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("tasks/{taskId:guid}/attachments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAttachment(
            Guid taskId,
            IFormFile file,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _attachmentService.UploadAttachmentAsync(taskId, userId, file, ct);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(ct);
                return HandleCreated(result, SuccessCodes.Task.AttachmentUploaded);
            }

            return HandleResult(result);
        }

        [HttpDelete("tasks/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DeleteAttachment(
            Guid attachmentId,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _attachmentService.DeleteAttachmentAsync(attachmentId, userId, ct);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return HandleResult(result, SuccessCodes.Task.AttachmentDeleted);
        }

        [HttpGet("tasks/{taskId:guid}/attachments")]
        public async Task<IActionResult> GetAttachments(
            Guid taskId,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var result = await _attachmentService.GetAttachmentsAsync(taskId, userId, ct);

            return HandleResult(result, SuccessCodes.Task.AttachmentsRetrieved);
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
