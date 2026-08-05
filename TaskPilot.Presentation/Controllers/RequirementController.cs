using System;
using System.Linq;
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

using Microsoft.AspNetCore.Authorization;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize(Roles = "ProjectManager")]
    [ApiController]
    [Route("api/requirements")]
    public class RequirementController : ApiControllerBase
    {
        private readonly RequirementsOrchestrator _orchestrator;
        private readonly DocumentIngestionOrchestrator _documentIngestionOrchestrator;
        private readonly IRequirementSessionStore _sessionStore;
        private readonly IVectorStore _vectorStore;
        private readonly IRequirementFinalizationService _finalizationService;
        private readonly RequirementDiscoveryOrchestrator _discoveryOrchestrator;
        private readonly IFileValidatorService _fileValidator;

        public RequirementController(
            RequirementsOrchestrator orchestrator,
            DocumentIngestionOrchestrator documentIngestionOrchestrator,
            RequirementDiscoveryOrchestrator discoveryOrchestrator,
            IRequirementSessionStore sessionStore,
            IVectorStore vectorStore,
            IRequirementFinalizationService finalizationService,
            IFileValidatorService fileValidator)
        {
            _orchestrator = orchestrator;
            _documentIngestionOrchestrator = documentIngestionOrchestrator;
            _discoveryOrchestrator = discoveryOrchestrator;
            _sessionStore = sessionStore;
            _vectorStore = vectorStore;
            _finalizationService = finalizationService;
            _fileValidator = fileValidator;
        }

        [HttpPost]
        public async Task<ActionResult> UnifiedEntry(
            [FromForm] RequirementDiscoveryRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                if (request.Documents != null && request.Documents.Any())
                {
                    foreach (var file in request.Documents)
                    {
                        var validationResult = await _fileValidator.ValidateAsync(
                            file,
                            new[] { FileType.Pdf, FileType.Docx, FileType.Txt },
                            15 * 1024 * 1024,
                            cancellationToken);
                        
                        if (!validationResult.IsSuccess)
                        {
                            return HandleResult(Result.Failure<RequirementDiscoveryResponse>(validationResult.Error!));
                        }
                    }
                }

                var response = await _discoveryOrchestrator.ExecuteAsync(request, cancellationToken);
                return HandleResult(Result.Success(response));
            }
            catch (Exception ex)
            {
                return HandleResult(Result.Failure<RequirementDiscoveryResponse>(
                    new Error("DISCOVERY_FAILED", ErrorType.Failure, ex.Message)));
            }
        }

        [Obsolete("Use the unified POST /api/requirements endpoint instead.")]
        [HttpPost("start-with-document")]
        public async Task<ActionResult> StartWithDocument(
            [FromForm] StartWithDocumentRequest request,
            CancellationToken cancellationToken)
        {
            if (request.File is null || request.File.Length == 0)
                return HandleResult(Result.Failure<DocumentStartResult>(
                    new Error("NO_FILE", ErrorType.Validation, "No file provided.")));

            var validationResult = await _fileValidator.ValidateAsync(
                request.File,
                new[] { FileType.Pdf, FileType.Docx, FileType.Txt },
                15 * 1024 * 1024,
                cancellationToken);

            if (!validationResult.IsSuccess)
            {
                return HandleResult(Result.Failure<DocumentStartResult>(validationResult.Error!));
            }

            // 1. Create session (document-first path)
            var session = await _orchestrator.StartWithDocumentAsync(cancellationToken);

            // 2. Ingest document into the new session
            var ingestionResult = await _documentIngestionOrchestrator
                .IngestAsync(session.SessionId, request.File, cancellationToken);

            if (!ingestionResult.Success)
                return HandleResult(Result.Failure<DocumentStartResult>(
                    new Error("DOCUMENT_INGESTION_FAILED", ErrorType.Failure, ingestionResult.Message)));

            // 3. Reload session (now contains gap questions + confidence scores)
            var updatedSession = await _sessionStore.GetAsync(session.SessionId, cancellationToken);

            var response = new DocumentStartResult
            {
                SessionId        = session.SessionId,
                Status           = updatedSession?.Status.ToString() ?? session.Status.ToString(),
                IsLimitedMode    = false,
                ConfidenceScores = updatedSession?.ConfidenceScores ?? new(),
                PendingQuestions = updatedSession?.UnansweredQuestions ?? new(),
                Message          = $"Document analyzed. {ingestionResult.QuestionsAutoResolved} existing " +
                                   $"questions resolved. Gap analysis complete."
            };

            return HandleResult(Result.Success(response));
        }

        [Obsolete("Use the unified POST /api/requirements endpoint instead.")]
        [HttpPost("document")]
        public async Task<ActionResult> Document(
            [FromForm] DocumentUploadRequest request,
            CancellationToken cancellationToken)
        {
            if (request.File == null || request.File.Length == 0)
                return HandleResult(Result.Failure<DocumentIngestionResult>(
                    new Error("NO_FILE", ErrorType.Validation, "No file provided.")));

            var validationResult = await _fileValidator.ValidateAsync(
                request.File,
                new[] { FileType.Pdf, FileType.Docx, FileType.Txt },
                    15 * 1024 * 1024,
                    cancellationToken);

                if (!validationResult.IsSuccess)
                {
                    return HandleResult(Result.Failure<DocumentIngestionResult>(validationResult.Error!));
                }

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

        [Obsolete("Use the unified POST /api/requirements endpoint instead.")]
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

        [HttpGet("{sessionId}/completeness")]
        public async Task<ActionResult> GetCompleteness(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
            if (session is null)
                return HandleResult(Result.Failure<TaskPilot.DTOs.AI.Requirements.RequirementCompletenessDTO>(CommonErrors.NotFound("Requirement session")));

            var report = session.RequirementCompletenessReport;
            if (report == null)
                return HandleResult(Result.Failure<TaskPilot.DTOs.AI.Requirements.RequirementCompletenessDTO>(CommonErrors.NotFound("Completeness report not found.")));

            var dto = new TaskPilot.DTOs.AI.Requirements.RequirementCompletenessDTO
            {
                OverallCompleteness = report.OverallCompleteness,
                Readiness = new TaskPilot.DTOs.AI.Requirements.ReadinessDTO { Status = report.Readiness },
                BlockingCategories = report.BlockingCategories,
                QuestionImpact = new TaskPilot.DTOs.AI.Requirements.QuestionImpactDTO
                {
                    HighPriorityQuestions = report.HighPriorityQuestions,
                    MediumPriorityQuestions = report.MediumQuestions,
                    LowPriorityQuestions = report.LowQuestions
                },
                MissingCriticalAreas = report.MissingCriticalAreas,
                ReadinessRecommendation = report.ReadinessRecommendation,
                BlockingFactors = report.BlockingFactors.Select(f => new TaskPilot.DTOs.AI.Requirements.BlockingFactorsDTO { Factor = f }).ToList(),
                EstimatedCompletenessAfterPendingQuestions = report.EstimatedCompletenessAfterPendingQuestions,
                ReadyForFinalization = report.ReadyForFinalization
            };

            return HandleResult(Result.Success(dto));
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