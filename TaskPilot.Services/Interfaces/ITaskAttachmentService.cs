using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Tasks.Attachments;

namespace TaskPilot.Services.Interfaces
{
    public interface ITaskAttachmentService
    {
        Task<Result<TaskAttachmentDto>> UploadAttachmentAsync(
            Guid taskId,
            Guid userId,
            IFormFile file,
            CancellationToken ct = default);

        Task<Result> DeleteAttachmentAsync(
            Guid attachmentId,
            Guid userId,
            CancellationToken ct = default);

        Task<Result<IEnumerable<TaskAttachmentDto>>> GetAttachmentsAsync(
            Guid taskId,
            Guid userId,
            CancellationToken ct = default);
    }
}
