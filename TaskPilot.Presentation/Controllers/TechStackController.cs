using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Projects;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/tech-stack")]
    public class TechStackController : ControllerBase
    {
        private readonly ITechStackService _techStackService;

        public TechStackController(ITechStackService techStackService)
        {
            _techStackService = techStackService;
        }

        [HttpGet("suggest")]
        public async Task<IActionResult> Suggest(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            try
            {
                var suggestion = await _techStackService
                    .SuggestAsync(projectId, cancellationToken);
                return Ok(suggestion);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmTechStackRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await _techStackService
                    .ConfirmAsync(projectId, request, cancellationToken);
                return Ok(new { message = "Tech stack confirmed successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
