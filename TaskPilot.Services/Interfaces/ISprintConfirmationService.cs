using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Sprints;

using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintConfirmationService
    {
        Task<Result<ConfirmSprintResult>> ConfirmAsync(
            Guid projectId,
            ConfirmSprintRequest request,
            CancellationToken cancellationToken = default);
    }
}
