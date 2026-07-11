using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.DTOs;

namespace TaskPilot.Services.Interfaces
{
    public interface IWbsGenerationService
    {
        Task<Result<WbsPersistenceResult>> GenerateAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
