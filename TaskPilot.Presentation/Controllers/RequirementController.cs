using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.DTOs.AI.Requirements;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/requirements")]
    public class RequirementController
        : ApiControllerBase
    {
        private readonly
            RequirementsOrchestrator
                _orchestrator;

        private readonly
            DocumentIngestionOrchestrator
                _documentIngestionOrchestrator;

        private readonly
            IRequirementSessionStore
                _sessionStore;

        private readonly
            IVectorStore
                _vectorStore;

        private readonly
            IRequirementFinalizationService
                _finalizationService;

        public RequirementController(
            RequirementsOrchestrator orchestrator,
            DocumentIngestionOrchestrator documentIngestionOrchestrator,
            IRequirementSessionStore sessionStore,
            IVectorStore vectorStore,
            IRequirementFinalizationService finalizationService)
        {
            _orchestrator =
                orchestrator;

            _documentIngestionOrchestrator =
                documentIngestionOrchestrator;

            _sessionStore =
                sessionStore;
                
            _vectorStore =
                vectorStore;
                
            _finalizationService =
                finalizationService;
        }

        [HttpPost("document")]
        public async Task<IActionResult>
            Document(
                [FromForm]
                DocumentUploadRequest request,
                CancellationToken cancellationToken)
        {
            var result =
                await _documentIngestionOrchestrator
                    .IngestAsync(
                        request.SessionId,
                        request.File,
                        cancellationToken);

            return Ok(result);
        }

        [HttpPost("message")]
        public async Task<IActionResult>
    Message(
        [FromBody]
        RequirementMessageRequest request,
        CancellationToken cancellationToken)
        {
            RequirementSession result;

            if (request.SessionId
                is null)
            {
                result =
                    await _orchestrator
                        .StartAsync(
                            request.Message,
                            cancellationToken);
            }
            else
            {
                result =
                    await _orchestrator
                        .ProcessPMResponseAsync(
                            request.SessionId.Value,
                            request.Message,
                            cancellationToken);
            }

            return Ok(result);
        }
        [HttpGet("{sessionId}")]
        public async Task<IActionResult>
            Get(
                Guid sessionId,
                CancellationToken cancellationToken)
        {
            var session =
                await _sessionStore
                    .GetAsync(
                        sessionId,
                        cancellationToken);

            if (session is null)
            {
                return NotFound();
            }

            return Ok(session);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] Guid sessionId,
            [FromQuery] string query,
            CancellationToken cancellationToken)
        {
            var results = await _vectorStore.SearchAsync(sessionId, query, cancellationToken: cancellationToken);
            return Ok(results);
        }

        [HttpPost("{sessionId}/finalize")]
        public async Task<IActionResult> Finalize(
            Guid sessionId,
            [FromBody] FinalizeRequirementsRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _finalizationService.FinalizeRequirementsAsync(sessionId, request, cancellationToken);
                return Ok(response);
            }
            catch (TaskPilot.Services.Exceptions.SessionAlreadyFinalizedException ex)
            {
                return Conflict(new { message = ex.Message, projectId = ex.ProjectId });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (TaskPilot.Services.Exceptions.UnprocessableEntityException ex)
            {
                return UnprocessableEntity(new { message = ex.Message });
            }
        }
    }
}