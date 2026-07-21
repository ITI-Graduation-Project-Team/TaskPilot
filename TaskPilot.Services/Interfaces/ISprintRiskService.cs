using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintRiskService
    {
        Task DetectAndPersistRisksAsync(Guid sprintId, CancellationToken ct = default);
        Task AnalyzeSprintBurnoutAsync(Guid sprintId, CancellationToken ct = default);
        Task<Result<TeamPulseDto>> GetTeamPulseAsync(Guid sprintId, CancellationToken ct = default);
        Task<Result<List<SprintRiskAlertDto>>> GetAlertsAsync(Guid sprintId);
        Task<Result> DismissAlertAsync(Guid alertId, Guid requestingUserId);
        Task<Result<SprintRiskSimulationResponseDto>> SimulateAsync(Guid alertId, CancellationToken ct = default);
    }
}
