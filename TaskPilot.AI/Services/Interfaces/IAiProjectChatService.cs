using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Chat;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IAiProjectChatService
    {
        Task<Result<ProjectChatSessionDto>> GetOrCreateSessionAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result> AppendUserMessageAsync(Guid projectId, string content, CancellationToken cancellationToken = default);
        Task<Result> AppendAssistantMessageAsync(Guid projectId, string content, CancellationToken cancellationToken = default);
        Task<Result> AppendMessagesAsync(Guid projectId, System.Collections.Generic.List<(string Role, string Content)> messages, CancellationToken cancellationToken = default);
    }
}
