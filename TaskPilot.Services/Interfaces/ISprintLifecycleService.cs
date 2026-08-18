using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Sprints;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintLifecycleService
    {
        Task<Result<SprintStatusDto>> StartSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default);
        Task<Result<SprintStatusDto>> CancelSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default);
        Task<Result<SprintStatusDto>> CompleteSprintAsync(Guid projectId, Guid sprintId, ReviewTaskAction? reviewAction = null, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<SprintListItemDto>>> GetAllSprintsPagedAsync(Guid projectId, Guid userId, int page, int pageSize, string? statusFilter, string? dateFrom, string? dateTo, string lang = "en", CancellationToken cancellationToken = default);
        Task<Result<ActiveSprintDto>> GetActiveSprintAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ActiveSprintDto>> GetPlannedSprintAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<LatestCompletedSprintDto>> GetLatestCompletedSprintAsync(Guid projectId);
        Task<Result<IEnumerable<SprintBoardTaskDto>>> GetSprintTasksAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<SprintBoardTaskDto>>> GetSprintTasksPagedAsync(Guid projectId, Guid sprintId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
        /// <summary>
        /// Completes a sprint only when it is due. Returns false when the sprint
        /// is cancelled, deleted, missing, or its end date has been moved forward.
        /// </summary>
        Task<bool> EnsureCompletedIfDueAsync(Guid sprintId, CancellationToken cancellationToken = default);
    }
}
