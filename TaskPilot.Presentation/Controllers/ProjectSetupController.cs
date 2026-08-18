using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId:guid}/setup")]
    [Authorize(Roles = "ProjectManager")]
    public sealed class ProjectSetupController(IProjectSetupService setupService) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult> Get(Guid projectId, CancellationToken cancellationToken)
            => HandleResult(await setupService.GetAsync(projectId, cancellationToken));

        [HttpGet("status")]
        public async Task<ActionResult> GetStatus(Guid projectId, CancellationToken cancellationToken)
            => HandleResult(await setupService.GetStatusAsync(projectId, cancellationToken));
    }
}
