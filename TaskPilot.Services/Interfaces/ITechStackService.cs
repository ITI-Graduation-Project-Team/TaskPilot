using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Planning;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ITechStackService
    {
        Task<Result<TechStackSuggestion>> SuggestAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);

        Task<Result> ConfirmAsync(
            Guid projectId,
            ConfirmTechStackRequest request,
            CancellationToken cancellationToken = default);
    }
}
