using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories.Interfaces
{
    public interface ISprintRepository : IRepository<Sprint>
    {
        Task<Sprint?> GetActiveSprintByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Sprint?> GetPlannedSprintByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Sprint?> GetSprintWithTasksAsync(Guid sprintId, CancellationToken cancellationToken = default);
    }
}
