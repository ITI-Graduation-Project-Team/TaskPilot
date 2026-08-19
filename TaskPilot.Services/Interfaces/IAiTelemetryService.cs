using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Telemetry;
using TaskPilot.Models.Common.Results;
using TaskPilot.AI.Models.Telemetry;

namespace TaskPilot.Services.Interfaces
{
    public interface IAiTelemetryService
    {
        Task LogTelemetryBatchAsync(
            IReadOnlyCollection<AiUsageRecord> records,
            CancellationToken cancellationToken = default);

        Task<Result<EmployeeAiSummaryDto>> GetEmployeeSummaryAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<AiTelemetryLogDto>>> GetEmployeeLogsAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);

        Task<Result<ProjectAiSummaryDto>> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ManagedProjectsAiSummaryDto>> GetManagedProjectsSummaryAsync(Guid managerId, CancellationToken cancellationToken = default);
        Task<Result<List<ProjectMemberAiUsageDto>>> GetProjectMemberBreakdownAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<AiTelemetryLogDto>>> GetProjectLogsAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);

        Task<Result<AdminAiDashboardDto>> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
        Task<Result<PagedResult<AiTelemetryLogDto>>> GetAdminLogsAsync(
            Guid? userId,
            string? operationType,
            string? status,
            string? modelName,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
