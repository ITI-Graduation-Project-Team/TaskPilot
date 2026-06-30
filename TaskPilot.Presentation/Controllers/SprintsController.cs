using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprints")]
    public class SprintsController : ControllerBase
    {
        private readonly ISprintConfirmationService _sprintConfirmationService;

        public SprintsController(ISprintConfirmationService sprintConfirmationService)
        {
            _sprintConfirmationService = sprintConfirmationService;
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmSprintRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sprintConfirmationService
                    .ConfirmAsync(projectId, request, cancellationToken);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (System.Collections.Generic.KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
