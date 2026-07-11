using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Sprint;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/sprints")]
    [ApiController]
    [Authorize]
    public class SprintRiskController : ControllerBase
    {
        private readonly ISprintRiskService _riskService;
        private readonly ICurrentUserService _currentUserService;

        public SprintRiskController(ISprintRiskService riskService, ICurrentUserService currentUserService)
        {
            _riskService = riskService;
            _currentUserService = currentUserService;
        }

        [HttpGet("{sprintId}/risks")]
        [ProducesResponseType(typeof(Result<List<SprintRiskAlertDto>>), 200)]
        public async Task<IActionResult> GetRisks(Guid sprintId)
        {
            var result = await _riskService.GetAlertsAsync(sprintId);
            return Ok(result);
        }

        [HttpPatch("{sprintId}/risks/{alertId}/dismiss")]
        [ProducesResponseType(typeof(Result), 200)]
        [ProducesResponseType(typeof(Result), 400)]
        public async Task<IActionResult> DismissRisk(Guid sprintId, Guid alertId)
        {
            var result = await _riskService.DismissAlertAsync(alertId, _currentUserService.UserId.Value);
            if (!result.IsSuccess)
                return BadRequest(result);
                
            return Ok(result);
        }

        [HttpGet("{sprintId}/risks/{alertId}/simulate")]
        [ProducesResponseType(typeof(Result<SprintRiskSimulationResponseDto>), 200)]
        [ProducesResponseType(typeof(Result<SprintRiskSimulationResponseDto>), 400)]
        public async Task<IActionResult> SimulateResolution(Guid sprintId, Guid alertId, CancellationToken ct)
        {
            var result = await _riskService.SimulateAsync(alertId, ct);
            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
