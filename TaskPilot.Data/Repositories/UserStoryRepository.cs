using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories
{
    public class UserStoryRepository : IUserStoryRepository
    {
        private readonly ApplicationDbContext _context;

        public UserStoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(
            IEnumerable<UserStory> userStories,
            CancellationToken cancellationToken = default)
        {
            await _context.UserStories.AddRangeAsync(userStories, cancellationToken);
        }

        public async Task<List<UserStory>> GetByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserStories
                .Include(u => u.Tasks)
                .Where(x => x.ProjectId == projectId)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserStory>> GetUnassignedByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserStories
                .Where(x => x.ProjectId == projectId && x.SprintId == null)
                .ToListAsync(cancellationToken);
        }
    }
}
