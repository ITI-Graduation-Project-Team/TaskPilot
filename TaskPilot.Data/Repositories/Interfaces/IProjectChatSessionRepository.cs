using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories.Interfaces
{
    public interface IProjectChatSessionRepository : IRepository<ProjectChatSession>
    {
        Task<ProjectChatSession?> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<ProjectChatSession?> GetByProjectIdWithMessagesAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
