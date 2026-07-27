using TaskPilot.DTOs.Sprint;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintRetrospectiveService
    {
        Task<SprintRetrospectiveDto> GenerateAsync(
            Guid projectId,
            Guid sprintId,
            string userLanguage,
            CancellationToken cancellationToken = default);

        Task<SprintRetrospectiveDto?> GetAsync(
            Guid sprintId,
            CancellationToken cancellationToken = default);
    }
}
