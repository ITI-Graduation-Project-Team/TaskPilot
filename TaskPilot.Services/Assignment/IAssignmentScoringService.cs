using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public interface IAssignmentScoringService
{
    Task<Result<ScoredAssignmentDto>> ScoreAsync(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken);
}
