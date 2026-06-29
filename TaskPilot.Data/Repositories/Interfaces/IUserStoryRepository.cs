using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories.Interfaces
{
    public interface IUserStoryRepository
    {
        Task AddRangeAsync(
            IEnumerable<UserStory> userStories,
            CancellationToken cancellationToken = default);

        Task<List<UserStory>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);

        Task<List<UserStory>> GetUnassignedByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);
    }
}
