using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;

namespace TaskPilot.Data.Repositories
{
    public class SprintRepository : Repository<Sprint>, ISprintRepository
    {
        public SprintRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Sprint?> GetActiveSprintByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Active && !s.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<Sprint?> GetSprintWithTasksAsync(Guid sprintId, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == sprintId && !s.IsDeleted, cancellationToken);
        }
    }
}
