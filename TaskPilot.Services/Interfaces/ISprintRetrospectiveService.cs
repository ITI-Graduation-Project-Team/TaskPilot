using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Sprint;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintRetrospectiveService
    {
        Task<Result<SprintRetrospectiveResponseDto>> GenerateRetrospectiveAsync(Guid sprintId, CancellationToken cancellationToken = default);
        Task<Result<SprintRetrospectiveResponseDto>> GetRetrospectiveAsync(Guid sprintId, CancellationToken cancellationToken = default);
    }
}
