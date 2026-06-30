using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IBacklogRegenerationService
    {
        Task<Result<RegenerationSummaryDto>> RegenerateBacklogAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
