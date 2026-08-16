using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface ITokenQuotaEnforcer
    {
        Task<(bool IsExceeded, long Limit, long CurrentUsage)> CheckQuotaAsync(CancellationToken cancellationToken = default);
        Task TrackTokensAsync(ChatMessageContent response, CancellationToken cancellationToken = default);
    }
}
