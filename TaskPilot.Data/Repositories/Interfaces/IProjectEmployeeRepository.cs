using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaskPilot.Data.Repositories.Interfaces
{
    public interface IProjectEmployeeRepository
    {
        Task<HashSet<Guid>> GetEmployeeIdsByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);
    }
}
