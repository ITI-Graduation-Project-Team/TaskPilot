using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Planning;
using TaskPilot.DTOs.Projects;

namespace TaskPilot.Services.Interfaces
{
    public interface ITechStackService
    {
        Task<TechStackSuggestion> SuggestAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);

        Task ConfirmAsync(
            Guid projectId,
            ConfirmTechStackRequest request,
            CancellationToken cancellationToken = default);
    }
}
