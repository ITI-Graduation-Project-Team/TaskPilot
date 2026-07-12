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
            if (request.RequirementSessionId == null && request.ProjectId == null && request.CompanyId == null)
            {
                return HandleResult(Result.Failure<KnowledgeAnswerResult>(KnowledgeErrors.MissingTenantIsolation));
            }

            var result = await _orchestrator.AskAsync(
                request.CollectionType,
                request.RequirementSessionId,
                request.ProjectId,
                request.CompanyId,
                request.Question,
                topK: 5,
                scoreThreshold: 0.75f,
                category: null,
                cancellationToken: cancellationToken);

            return HandleResult(result);
        }
    }
}
