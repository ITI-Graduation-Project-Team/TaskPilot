using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Services.DTOs;

namespace TaskPilot.Services.Interfaces
{
    public interface IWbsPersistenceService
    {
        Task<WbsPersistenceResult> PersistAsync(
            Guid projectId,
            GeneratedWbs wbs,
            CancellationToken cancellationToken = default);
    }
}
