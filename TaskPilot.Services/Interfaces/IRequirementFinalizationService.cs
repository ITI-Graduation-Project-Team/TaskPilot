using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI.Requirements;

namespace TaskPilot.Services.Interfaces
{
    public interface IRequirementFinalizationService
    {
        Task<FinalizeRequirementsResponse> FinalizeRequirementsAsync(Guid sessionId, FinalizeRequirementsRequest request, CancellationToken cancellationToken = default);
    }
}
