using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Orchestrators;
using TaskPilot.DTOs.AI.Knowledge;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.AI.Models.RAG;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/knowledge")]
    public class KnowledgeController : ApiControllerBase
    {
        private readonly KnowledgeOrchestrator _orchestrator;

        public KnowledgeController(KnowledgeOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        [HttpPost("ask")]
        public async Task<ActionResult> AskAsync(
            [FromBody] KnowledgeAskRequest request,
            CancellationToken cancellationToken)
        {
            if (request.SessionId == Guid.Empty)
            {
                return HandleResult(Result.Failure<KnowledgeAnswerResult>(KnowledgeErrors.EmptySessionId));
            }

            var result = await _orchestrator.AskAsync(
                request.SessionId,
                request.Question,
                topK: 5,
                category: null,
                cancellationToken: cancellationToken);

            return HandleResult(Result.Success(result));
        }
    }
}
