using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Models.ContextAdvisor;
using TaskPilot.AI.Orchestrators;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/context-advisor")]
    public class ContextAdvisorController : ApiControllerBase
    {
        private readonly ContextAdvisorOrchestrator _contextAdvisorOrchestrator;
        private readonly DocumentIngestionOrchestrator _documentIngestionOrchestrator;

        public ContextAdvisorController(
            ContextAdvisorOrchestrator contextAdvisorOrchestrator,
            DocumentIngestionOrchestrator documentIngestionOrchestrator)
        {
            _contextAdvisorOrchestrator = contextAdvisorOrchestrator;
            _documentIngestionOrchestrator = documentIngestionOrchestrator;
        }

        [HttpPost("documents")]
        public async Task<IActionResult> UploadProjectKnowledge(
            [FromForm] ProjectKnowledgeUploadRequest request,
            CancellationToken cancellationToken)
        {
            var result =
                await _documentIngestionOrchestrator
                    .IngestProjectKnowledgeAsync(
                        request.File,
                        request.ProjectId,
                        request.IsAvailableToContextSummarizer,
                        cancellationToken);

            return Ok(result);
        }

        [HttpPost("summary")]
        public async Task<IActionResult> GetContextSummary(
            [FromBody] TaskContextRequest request,
            CancellationToken cancellationToken)
        {
            var result =
                await _contextAdvisorOrchestrator
                    .GenerateSummaryAsync(request, cancellationToken);

            return Ok(result);
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask(
            [FromBody] ContextAdvisorChatRequest request,
            CancellationToken cancellationToken)
        {
            var result =
                await _contextAdvisorOrchestrator
                    .AskAsync(request, cancellationToken);

            return Ok(result);
        }
    }
}
