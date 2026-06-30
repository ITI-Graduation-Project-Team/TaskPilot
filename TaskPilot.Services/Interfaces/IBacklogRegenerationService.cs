using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Backlog;

namespace TaskPilot.Services.Interfaces
{
    public interface IBacklogRegenerationService
    {
        Task<RegenerationSummaryDto> RegenerateBacklogAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
