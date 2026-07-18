using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Chat;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IProjectChatService
    {
        Task<Result<ProjectChatSessionDto>> GetOrCreateSessionAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ProjectChatSessionDto>> GetSessionAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result> AppendUserMessageAsync(Guid projectId, string content, CancellationToken cancellationToken = default);
        Task<Result> AppendAssistantMessageAsync(Guid projectId, string content, CancellationToken cancellationToken = default);
        Task<Result> AppendMessagesAsync(Guid projectId, List<(string Role, string Content)> messages, CancellationToken cancellationToken = default);
    }
}
