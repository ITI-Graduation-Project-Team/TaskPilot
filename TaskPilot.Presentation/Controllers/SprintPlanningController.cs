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
    public class SprintPlanningController : ApiControllerBase
    {
        private readonly ISprintPlanningService _sprintPlanningService;

        public SprintPlanningController(ISprintPlanningService sprintPlanningService)
        {
            _sprintPlanningService = sprintPlanningService;
        }

        [HttpPost]
        public async Task<ActionResult> GenerateSprintSuggestion(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _sprintPlanningService.GenerateSprintSuggestionAsync(projectId, cancellationToken);
            return HandleResult(result);
        }
    }
}
