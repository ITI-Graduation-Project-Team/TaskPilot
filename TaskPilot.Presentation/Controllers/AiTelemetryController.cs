using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/ai-telemetry")]
    [Authorize]
    public class AiTelemetryController : ApiControllerBase
    {
        private readonly IAiTelemetryService _telemetryService;
        private readonly ICurrentUserService _currentUserService;

        public AiTelemetryController(IAiTelemetryService telemetryService, ICurrentUserService currentUserService)
        {
            _telemetryService = telemetryService;
            _currentUserService = currentUserService;
        }

        // ──────────────────────── Employee Endpoints ────────────────────────

        [HttpGet("employee/summary")]
        [Authorize(Roles = "Employee")]
        public async Task<ActionResult> GetEmployeeSummary(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            if (userId == null || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await _telemetryService.GetEmployeeSummaryAsync(userId.Value, cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }

        [HttpGet("employee/logs")]
        [Authorize(Roles = "Employee")]
        public async Task<ActionResult> GetEmployeeLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;
            if (userId == null || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var result = await _telemetryService.GetEmployeeLogsAsync(userId.Value, page, pageSize, cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }

        // ──────────────────────── Project Manager Endpoints ────────────────────────

        [HttpGet("projects/{projectId:guid}/summary")]
        [Authorize(Roles = "ProjectManager,Admin")]
        public async Task<ActionResult> GetProjectSummary(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _telemetryService.GetProjectSummaryAsync(projectId, cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }

        [HttpGet("projects/{projectId:guid}/members")]
        [Authorize(Roles = "ProjectManager,Admin")]
        public async Task<ActionResult> GetProjectMemberBreakdown(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _telemetryService.GetProjectMemberBreakdownAsync(projectId, cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }

        [HttpGet("projects/{projectId:guid}/logs")]
        [Authorize(Roles = "ProjectManager,Admin")]
        public async Task<ActionResult> GetProjectLogs(
            Guid projectId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _telemetryService.GetProjectLogsAsync(projectId, page, pageSize, cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }

        // ──────────────────────── Admin Endpoints ────────────────────────

        [HttpGet("admin/dashboard")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAdminDashboard(CancellationToken cancellationToken)
        {
            var result = await _telemetryService.GetAdminDashboardAsync(cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }

        [HttpGet("admin/logs")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAdminLogs(
            [FromQuery] Guid? userId = null,
            [FromQuery] string? operationType = null,
            [FromQuery] string? status = null,
            [FromQuery] string? modelName = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _telemetryService.GetAdminLogsAsync(userId, operationType, status, modelName, page, pageSize, cancellationToken);
            return HandleResult(result, SuccessCodes.Telemetry.Retrieved);
        }
    }
}
