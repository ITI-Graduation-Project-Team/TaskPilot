using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

namespace TaskPilot.Presentation.Controllers
{
    /// <summary>
    /// Exposes the two-step AI project generation workflow.
    ///
    /// Step 1 — POST /api/aiproject/generate
    ///   Accepts requirements (text + optional audio + optional document),
    ///   calls OpenAI to produce a project draft, and returns it to the PM for review.
    ///   Nothing is written to the database at this stage.
    ///
    /// Step 2 — POST /api/aiproject/confirm
    ///   The PM sends back the (possibly edited) draft.
    ///   The project is validated and persisted to the database.
    /// </summary>
    [Authorize(Roles = "ProjectManager")]
    public class AiProjectController : ApiControllerBase
    {
        private readonly IAiProjectGeneratorService _generatorService;
        private readonly IAudioTranscriptionService _transcriptionService;
        private readonly IFileTextExtractor _fileTextExtractor;
        private readonly IUnitOfWork _unitOfWork;

        public AiProjectController(
            IAiProjectGeneratorService generatorService,
            IAudioTranscriptionService transcriptionService,
            IFileTextExtractor fileTextExtractor,
            IUnitOfWork unitOfWork)
        {
            _generatorService    = generatorService;
            _transcriptionService = transcriptionService;
            _fileTextExtractor   = fileTextExtractor;
            _unitOfWork          = unitOfWork;
        }

        // ── Step 1: Generate (preview only — no DB write) ────────────────────────────

        /// <summary>
        /// Generates a project draft from the supplied requirements.
        /// Returns the draft to the Project Manager for review before saving.
        /// </summary>
        [HttpPost("generate")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> Generate(
            [FromForm] ProjectGeneratorRequestDTO request,
            IFormFile? audioFile,
            IFormFile? documentFile)
        {
            var requirementsBuilder = new System.Text.StringBuilder();

            // 1. Append free-text requirements
            if (!string.IsNullOrWhiteSpace(request.TextRequirements))
                requirementsBuilder.AppendLine(request.TextRequirements);

            // 2. Transcribe audio file and append
            if (audioFile is not null)
            {
                var transcriptionResult = await _transcriptionService.TranscribeAsync(audioFile);

                if (!transcriptionResult.IsSuccess)
                    return HandleResult(transcriptionResult);

                requirementsBuilder.AppendLine(transcriptionResult.Value);
            }

            // 3. Extract text from uploaded document and append
            if (documentFile is not null)
            {
                var documentText = await _fileTextExtractor.ExtractTextAsync(documentFile);

                if (string.IsNullOrWhiteSpace(documentText))
                    return HandleResult(
                        Result.Failure<string>(
                            CommonErrors.InvalidInput("The uploaded document appears to be empty.")));

                requirementsBuilder.AppendLine(documentText);
            }

            var combinedRequirements = requirementsBuilder.ToString().Trim();

            if (string.IsNullOrEmpty(combinedRequirements))
                return HandleResult(
                    Result.Failure<string>(
                        CommonErrors.InvalidInput(
                            "Please provide at least one of: text requirements, audio file, or document file.")));

            // 4. Ask AI to generate the project draft
            var result = await _generatorService.GenerateProjectAsync(
                combinedRequirements,
                request.CompanyId,
                request.ManagerId);

            return HandleResult(result);
        }

        // ── Step 2: Confirm (PM approves — project is saved) ─────────────────────────

        /// <summary>
        /// Persists the PM-approved (and optionally edited) project draft to the database.
        /// </summary>
        [HttpPost("confirm")]
        public async Task<ActionResult> Confirm([FromBody] GeneratedProjectDTO dto)
        {
            var result = await _generatorService.ConfirmProjectAsync(dto);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, "Project created successfully.");
        }
    }
}
