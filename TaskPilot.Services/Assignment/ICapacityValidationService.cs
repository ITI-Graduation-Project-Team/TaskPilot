using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Assignment;

public interface ICapacityValidationService
{
    Task<Result<CapacityValidationResult>> ValidateAsync(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken = default);
}
