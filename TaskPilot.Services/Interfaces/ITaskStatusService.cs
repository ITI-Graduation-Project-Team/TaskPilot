using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Tasks;

namespace TaskPilot.Services.Interfaces
{
    public interface ITaskStatusService
    {
        Task<Result<MyTasksSummaryDto>> GetMyTasksAsync(
            Guid projectId,
            Guid currentUserId,
            CancellationToken cancellationToken = default);

        Task<Result<TaskStatusUpdateResult>> UpdateStatusAsync(
            Guid taskId,
            Guid currentUserId,
            UpdateTaskStatusRequest request,
            CancellationToken cancellationToken = default);

        Task<Result<TaskStatusUpdateResult>> LogActualHoursAsync(
            Guid taskId,
            Guid currentUserId,
            LogActualHoursRequest request,
            CancellationToken cancellationToken = default);

        Task<Result<TaskStatusUpdateResult>> PmRejectReviewAsync(
            Guid taskId,
            Guid currentUserId,
            PmRejectReviewRequest request,
            CancellationToken cancellationToken = default);

        Task<Result<TaskStatusUpdateResult>> PmReopenTaskAsync(
            Guid taskId,
            Guid currentUserId,
            PmReopenTaskRequest request,
            CancellationToken cancellationToken = default);

        Task<Result<MyTasksSummaryDto>> GetMySprintTasksAsync(
            Guid projectId,
            Guid sprintId,
            Guid currentUserId,
            CancellationToken cancellationToken = default);
    }
}
