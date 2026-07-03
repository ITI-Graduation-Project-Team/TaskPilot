using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public interface ITeamSnapshotService
{
    Task<Result<SprintAssignmentSnapshotDto>> GetSnapshotAsync(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken = default);
}
