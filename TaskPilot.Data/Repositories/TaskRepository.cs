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

        public async Task<List<TaskItem>> GetBySprintIdAsync(
            Guid sprintId,
            CancellationToken cancellationToken = default)
        {
            return await _context.TaskItems
                .Where(t => t.SprintId == sprintId)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<TaskItem>> GetAssignedTasksBySprintAsync(
            Guid sprintId,
            Guid employeeId,
            CancellationToken cancellationToken = default)
        {
            return await _context.TaskItems
                .AsNoTracking()
                .Include(t => t.UserStory)
                .Include(t => t.RequiredSkills)
                    .ThenInclude(rs => rs.Skill)
                .Where(t => t.SprintId == sprintId && t.EmployeeId == employeeId)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.Status)
                .ToListAsync(cancellationToken);
        }

        public async Task<TaskItem?> GetByIdWithSprintAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            return await _context.TaskItems
                .Include(t => t.Sprint)
                .Include(t => t.UserStory)
                .Include(t => t.RequiredSkills)
                    .ThenInclude(rs => rs.Skill)
                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        }

        public void AddOverrideLog(TaskStatusOverrideLog log)
        {
            _context.TaskStatusOverrideLogs.Add(log);
        }
    }
}
