using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories.Interfaces
{
    public interface ITaskRepository
    {
        Task AddRangeAsync(
            IEnumerable<TaskItem> tasks,
            CancellationToken cancellationToken = default);

        Task<List<TaskItem>> GetByUserStoryIdAsync(
            Guid userStoryId,
            CancellationToken cancellationToken = default);
    }
}
