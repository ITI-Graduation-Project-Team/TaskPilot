using TaskPilot.DTOs.Planning;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintPlanningService
    {
        Task<SprintSuggestionDto> GenerateSprintSuggestionAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
