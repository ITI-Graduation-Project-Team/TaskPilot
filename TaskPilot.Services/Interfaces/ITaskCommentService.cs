using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Tasks.Comments;

namespace TaskPilot.Services.Interfaces
{
    public interface ITaskCommentService
    {
        Task<Result<TaskCommentDto>> AddCommentAsync(
            Guid taskId,
            Guid userId,
            AddTaskCommentRequest request,
            CancellationToken ct = default);

        Task<Result<TaskCommentDto>> UpdateCommentAsync(
            Guid commentId,
            Guid userId,
            UpdateTaskCommentRequest request,
            CancellationToken ct = default);

        Task<Result> DeleteCommentAsync(
            Guid commentId,
            Guid userId,
            CancellationToken ct = default);

        Task<Result<IEnumerable<TaskCommentDto>>> GetCommentsAsync(
            Guid taskId,
            Guid userId,
            CancellationToken ct = default);
    }
}
