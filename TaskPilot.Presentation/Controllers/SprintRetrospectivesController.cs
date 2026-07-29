using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId:guid}/sprints/{sprintId:guid}/retrospective")]
    public class SprintRetrospectivesController(
        ISprintRetrospectiveService retrospectiveService,
        IUnitOfWork unitOfWork) : ApiControllerBase
    {
        [HttpPost]
        [HttpPost("/api/sprints/{sprintId:guid}/retrospective/generate")]
        public async Task<IActionResult> Generate(
            [FromRoute] Guid projectId,
            [FromRoute] Guid sprintId,
            CancellationToken cancellationToken)
        {
            var userLanguage = HttpContext.Items["userLanguage"]?.ToString() ?? "English";

            var result = await retrospectiveService.GenerateAsync(
                projectId, sprintId, userLanguage, cancellationToken);
                
            await unitOfWork.SaveChangesAsync();

            return Ok(result);
        }

        [HttpGet]
        [HttpGet("/api/sprints/{sprintId:guid}/retrospective")]
        public async Task<IActionResult> Get(
            [FromRoute] Guid projectId,
            [FromRoute] Guid sprintId,
            CancellationToken cancellationToken)
        {
            var result = await retrospectiveService.GetAsync(sprintId, cancellationToken);

            if (result is null)
                return NotFound("No retrospective generated for this sprint yet.");

            return Ok(result);
        }
    }
}
