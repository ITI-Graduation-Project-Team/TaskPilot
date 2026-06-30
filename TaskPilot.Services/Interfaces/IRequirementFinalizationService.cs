using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI.Requirements;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IRequirementFinalizationService
    {
        Task<Result<FinalizeRequirementsResponse>> FinalizeRequirementsAsync(Guid sessionId, FinalizeRequirementsRequest request, CancellationToken cancellationToken = default);
    }
}
