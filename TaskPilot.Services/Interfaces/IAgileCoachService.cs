using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI.AgileCoach;
using TaskPilot.DTOs.AgileCoach;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IAgileCoachService
    {
        Task<Result<AgileCoachSummaryServiceResult>> GetOrGenerateSummaryAsync(Guid taskId, string lang, CancellationToken cancellationToken = default);
        Task<Result<AgileCoachSummaryServiceResult>> RegenerateSummaryAsync(Guid taskId, string lang, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> StreamChatAsync(Guid taskId, string userMessage, List<ChatMessageDto> history, string lang);
        Task<Result<List<AgileCoachChatMessageDto>>> GetChatHistoryAsync(Guid taskId, string lang);
        Task<Result> SaveChatMessageAsync(Guid taskId, string role, string content, string lang);
    }
}
