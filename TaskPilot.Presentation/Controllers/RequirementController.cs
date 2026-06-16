using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.DTOs.AI.Requirements;
using TaskPilot.AI.Models.Ingestion;

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

        public RequirementController(
            RequirementsOrchestrator orchestrator,
            DocumentIngestionOrchestrator documentIngestionOrchestrator,
            IRequirementSessionStore sessionStore)
        {
            _orchestrator =
                orchestrator;

            _documentIngestionOrchestrator =
                documentIngestionOrchestrator;

            _sessionStore =
                sessionStore;
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
    }
}