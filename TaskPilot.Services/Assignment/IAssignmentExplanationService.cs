using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public interface IAssignmentExplanationService
{
    Task<Result<ExplainedAssignmentDto>> GenerateAsync(
        Guid projectId,
        Guid sprintId,
        string language,
        CancellationToken cancellationToken);
}
