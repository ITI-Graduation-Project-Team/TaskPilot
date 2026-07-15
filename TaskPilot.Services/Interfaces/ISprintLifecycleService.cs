using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintLifecycleService
    {
        Task<Result<SprintStatusDto>> StartSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default);
        Task<Result<SprintStatusDto>> CompleteSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default);
        Task<Result<ActiveSprintDto>> GetActiveSprintAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ActiveSprintDto>> GetPlannedSprintAsync(Guid projectId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Completes a sprint only when it is due. Returns false when the sprint
        /// is cancelled, deleted, missing, or its end date has been moved forward.
        /// </summary>
        Task<bool> EnsureCompletedIfDueAsync(Guid sprintId, CancellationToken cancellationToken = default);
    }
}
