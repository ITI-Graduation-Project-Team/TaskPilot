using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories.Interfaces
{
    public interface IProjectEmployeeRepository
    {
        Task<HashSet<Guid>> GetEmployeeIdsByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);

        Task<bool> IsProjectManagerAsync(
            Guid projectId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<List<ProjectEmployee>> GetActiveByEmployeeIdAsync(
            Guid employeeId,
            CancellationToken cancellationToken = default);

        Task<List<ProjectEmployee>> GetActiveByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);
    }
}
