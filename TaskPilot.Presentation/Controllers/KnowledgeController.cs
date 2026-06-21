using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Orchestrators;
using TaskPilot.DTOs.AI.Knowledge;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/knowledge")]
    public class KnowledgeController : ControllerBase
    {
        private readonly KnowledgeOrchestrator _orchestrator;

        public KnowledgeController(KnowledgeOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskAsync(
            [FromBody] KnowledgeAskRequest request,
            CancellationToken cancellationToken)
        {
            if (request.SessionId == Guid.Empty)
            {
                return BadRequest("SessionId cannot be empty.");
            }

            var result = await _orchestrator.AskAsync(
                request.SessionId,
                request.Question,
                topK: 5,
                category: null,
                cancellationToken: cancellationToken);

            return Ok(result);
        }
    }
}
