using TaskPilot.DTOs.Planning;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintPlanningService
    {
        Task<Result<SprintSuggestionDto>> GenerateSprintSuggestionAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
