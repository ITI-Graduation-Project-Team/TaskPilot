using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprints")]
    public class SprintsController : ApiControllerBase
    {
        private readonly ISprintConfirmationService _sprintConfirmationService;

        public SprintsController(ISprintConfirmationService sprintConfirmationService)
        {
            _sprintConfirmationService = sprintConfirmationService;
        }

        [HttpPost("confirm")]
        public async Task<ActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmSprintRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _sprintConfirmationService
                .ConfirmAsync(projectId, request, cancellationToken);

            return HandleResult(result);
        }
    }
}
