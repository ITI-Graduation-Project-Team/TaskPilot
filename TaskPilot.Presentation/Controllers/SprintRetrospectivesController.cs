using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/sprints/{sprintId:guid}/retrospective")]
    public class SprintRetrospectivesController(
        ISprintRetrospectiveService retrospectiveService,
        IUnitOfWork unitOfWork) : ApiControllerBase
    {
        [HttpPost("generate")]
        public async Task<ActionResult> Generate(Guid sprintId, CancellationToken cancellationToken)
        {
            var result = await retrospectiveService.GenerateRetrospectiveAsync(sprintId, cancellationToken);
            
            if (result.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync();
            }

            return HandleResult(result);
        }

        [HttpGet]
        public async Task<ActionResult> Get(Guid sprintId, CancellationToken cancellationToken)
        {
            var result = await retrospectiveService.GetRetrospectiveAsync(sprintId, cancellationToken);
            return HandleResult(result);
        }
    }
}
