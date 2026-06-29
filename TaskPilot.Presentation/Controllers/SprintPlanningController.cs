using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprint-suggestions")]
    public class SprintPlanningController : ControllerBase
    {
        private readonly ISprintPlanningService _sprintPlanningService;

        public SprintPlanningController(ISprintPlanningService sprintPlanningService)
        {
            _sprintPlanningService = sprintPlanningService;
        }

        [HttpPost]
        public async Task<IActionResult> GenerateSprintSuggestion(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            try
            {
                var suggestion = await _sprintPlanningService.GenerateSprintSuggestionAsync(projectId, cancellationToken);
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
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while generating sprint suggestion: {ex.Message}");
            }
        }
    }
}
