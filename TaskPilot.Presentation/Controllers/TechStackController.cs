using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Projects;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using Microsoft.AspNetCore.Authorization;
using TaskPilot.Presentation.Models;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/tech-stack")]
    [Authorize(Roles = "ProjectManager")]
    public class TechStackController : ApiControllerBase
    {
        private readonly IProjectSetupService _setupService;

        public TechStackController(IProjectSetupService setupService)
        {
            _setupService = setupService;
        }

        [HttpGet("suggest")]
        public async Task<ActionResult> Suggest(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _setupService
                .GenerateTechStackSuggestionAsync(projectId, false, cancellationToken);
            return result.IsSuccess
                ? Ok(ApiResponse.Success(result.Value.TechStack.Suggestion))
                : HandleResult(result);
        }

        [HttpPost("suggestion")]
        public async Task<ActionResult> CreateSuggestion(Guid projectId, CancellationToken cancellationToken)
            => HandleResult(await _setupService.GenerateTechStackSuggestionAsync(projectId, false, cancellationToken));

        [HttpPost("suggestion/regenerate")]
        public async Task<ActionResult> RegenerateSuggestion(Guid projectId, CancellationToken cancellationToken)
            => HandleResult(await _setupService.GenerateTechStackSuggestionAsync(projectId, true, cancellationToken));

        [HttpPost("confirm")]
        public async Task<ActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmTechStackRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _setupService
                .ConfirmTechStackAsync(projectId, request, cancellationToken);
            return HandleResult(result, "Tech stack confirmed successfully.");
        }
    }
}
