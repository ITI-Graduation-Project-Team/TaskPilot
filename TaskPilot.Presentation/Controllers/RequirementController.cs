using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.DTOs.AI.Requirements;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Enums;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/requirements")]
    public class RequirementController : ApiControllerBase
    {
        private readonly RequirementsOrchestrator _orchestrator;
        private readonly DocumentIngestionOrchestrator _documentIngestionOrchestrator;
        private readonly IRequirementSessionStore _sessionStore;
        private readonly IVectorStore _vectorStore;
        private readonly IRequirementFinalizationService _finalizationService;

        public RequirementController(
            RequirementsOrchestrator orchestrator,
            DocumentIngestionOrchestrator documentIngestionOrchestrator,
            IRequirementSessionStore sessionStore,
            IVectorStore vectorStore,
            IRequirementFinalizationService finalizationService)
        {
            _orchestrator = orchestrator;
            _documentIngestionOrchestrator = documentIngestionOrchestrator;
            _sessionStore = sessionStore;
            _vectorStore = vectorStore;
            _finalizationService = finalizationService;
        }

        [HttpPost("document")]
        public async Task<ActionResult> Document(
            [FromForm] DocumentUploadRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _documentIngestionOrchestrator
                .IngestAsync(
                    request.SessionId,
                    request.File,
                    cancellationToken);

            if (!result.Success)
            {
                return HandleResult(Result.Failure<DocumentIngestionResult>(
                    new Error("DOCUMENT_INGESTION_FAILED", ErrorType.Failure, result.Message)));
            }

            return HandleResult(Result.Success(result));
        }

        [HttpPost("message")]
        public async Task<ActionResult> Message(
            [FromBody] RequirementMessageRequest request,
            CancellationToken cancellationToken)
        {
            RequirementSession session;

            if (request.SessionId is null)
            {
                session = await _orchestrator
                    .StartAsync(
                        request.Message,
                        cancellationToken);
            }
            else
            {
                session = await _orchestrator
                    .ProcessPMResponseAsync(
                        request.SessionId.Value,
                        request.Message,
                        cancellationToken);
            }

            return HandleResult(Result.Success(session));
        }

        [HttpGet("{sessionId}")]
        public async Task<ActionResult> Get(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var session = await _sessionStore
                .GetAsync(
                    sessionId,
                    cancellationToken);

            if (session is null)
            {
                return HandleResult(Result.Failure<RequirementSession>(CommonErrors.NotFound("Requirement session")));
            }

            return HandleResult(Result.Success(session));
        }

        [HttpGet("search")]
        public async Task<ActionResult> Search(
            [FromQuery] Guid sessionId,
            [FromQuery] string query,
            CancellationToken cancellationToken)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
            if (session == null)
            {
                return HandleResult(Result.Failure<System.Collections.Generic.List<KnowledgeChunk>>(CommonErrors.NotFound("Requirement session")));
            }
            var results = await _vectorStore.SearchAsync(
                KnowledgeCollectionType.ProjectPolicies,
                requirementSessionId: sessionId,
                projectId: null,
                companyId: null,
                queryText: query,
                cancellationToken: cancellationToken);
            return HandleResult(Result.Success(results));
        }

        [HttpPost("{sessionId}/finalize")]
        public async Task<ActionResult> Finalize(
            Guid sessionId,
            [FromBody] FinalizeRequirementsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _finalizationService.FinalizeRequirementsAsync(sessionId, request, cancellationToken);
            return HandleResult(result);
        }
    }
}