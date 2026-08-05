using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ICapacityCalculationService
    {
        Task<Result<SprintCapacityResult>> CalculateTargetSprintHoursAsync(Guid projectId, DateTime sprintStartDate, DateTime sprintEndDate, CancellationToken cancellationToken = default);
    }
}
