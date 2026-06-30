using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Sprints;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintConfirmationService
    {
        Task<ConfirmSprintResult> ConfirmAsync(
            Guid projectId,
            ConfirmSprintRequest request,
            CancellationToken cancellationToken = default);
    }
}
