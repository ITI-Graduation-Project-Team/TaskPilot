using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories
{
    public class ProjectChatSessionRepository : Repository<ProjectChatSession>, IProjectChatSessionRepository
    {
        public ProjectChatSessionRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ProjectChatSession?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);
        }

        public async Task<ProjectChatSession?> GetByProjectIdWithMessagesAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Include(s => s.Messages.OrderBy(m => m.SequenceIndex))
                .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);
        }
    }
}
