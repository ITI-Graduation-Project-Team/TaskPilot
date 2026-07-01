using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Projects;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/tech-stack")]
    public class TechStackController : ApiControllerBase
    {
        private readonly ITechStackService _techStackService;

        public TechStackController(ITechStackService techStackService)
        {
            _techStackService = techStackService;
        }

        [HttpGet("suggest")]
        public async Task<ActionResult> Suggest(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _techStackService
                .SuggestAsync(projectId, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("confirm")]
        public async Task<ActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmTechStackRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _techStackService
                .ConfirmAsync(projectId, request, cancellationToken);
            return HandleResult(result, SuccessCodes.TechStack.Confirmed);
        }
    }
}
