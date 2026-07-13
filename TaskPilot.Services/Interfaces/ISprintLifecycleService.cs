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
    }
}
