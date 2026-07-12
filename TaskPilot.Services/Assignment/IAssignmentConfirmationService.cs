using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public interface IAssignmentConfirmationService
{
    Task<Result<AssignmentConfirmationResult>> ConfirmAsync(
        Guid projectId,
        Guid sprintId,
        ConfirmAssignmentsRequest request,
        CancellationToken cancellationToken = default);
}
