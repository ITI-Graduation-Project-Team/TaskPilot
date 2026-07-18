using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaskPilot.AI.Orchestrators
{
    public interface IProjectAiChatOrchestrator
    {
        Task<string> ProcessBacklogChatAsync(Guid projectId, string message, CancellationToken cancellationToken = default);
        Task<string> ConfirmBacklogUpdatesAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
