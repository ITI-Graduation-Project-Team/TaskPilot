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
    public class TaskRepository : ITaskRepository
    {
        private readonly ApplicationDbContext _context;

        public TaskRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(
            IEnumerable<TaskItem> tasks,
            CancellationToken cancellationToken = default)
        {
            await _context.TaskItems.AddRangeAsync(tasks, cancellationToken);
        }

        public async Task<List<TaskItem>> GetByUserStoryIdAsync(
            Guid userStoryId,
            CancellationToken cancellationToken = default)
        {
            return await _context.TaskItems
                .Where(x => x.UserStoryId == userStoryId)
                .ToListAsync(cancellationToken);
        }
    }
}
