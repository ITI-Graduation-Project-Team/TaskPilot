using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Assistants;
using System.Text.Json;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

#pragma warning disable OPENAI001 // AssistantClient is experimental in some SDK versions

namespace TaskPilot.Services.AiProjectGenerator
{
    /// <summary>
    /// Generates a project draft from natural-language requirements via OpenAI Assistants API.
    ///
    /// Conversation history is stored in an <b>OpenAI Thread</b> — nothing is kept on the server.
    /// The PM receives a <c>chatId</c> (the OpenAI thread ID, e.g. "thread_abc123") and sends
    /// it back on subsequent calls. Each turn only sends the new message.
    ///
    /// Two-step flow:
    ///   1. <see cref="GenerateProjectAsync"/> — continues/starts the thread, returns draft or questions.
    ///   2. <see cref="ConfirmProjectAsync"/> — persists the approved draft (controller calls SaveChangesAsync).
    /// </summary>
    public class AiProjectGeneratorService : IAiProjectGeneratorService
    {
        private readonly AssistantClient _assistantClient;
        private readonly string _assistantId;
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<Company> _companyRepo;
        private readonly UserManager<User> _userManager;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private const string SystemPrompt = """
            You are a senior project manager with 15+ years of experience delivering software projects.
            Your job is to help create a well-scoped project definition — but ONLY when the requirements
            give you enough information to do so responsibly.

            ## YOUR DECISION PROCESS

            Carefully read the conversation so far and ask yourself:
              - Do I know the PURPOSE / business goal of this project?
              - Do I have a sense of the TARGET USERS or stakeholders?
              - Is the SCOPE clear enough (what's in vs. out)?
              - Are there any critical AMBIGUITIES that would produce a misleading project name/description?

            ## WHEN TO ASK FOR CLARIFICATION

            If the requirements are vague, contradictory, or missing critical context, you MUST ask
            clarification questions instead of guessing. Ask 2–5 focused, specific questions that
            will genuinely change how you define the project. Do NOT ask generic or obvious questions.

            ## WHEN TO GENERATE THE DRAFT

            Only generate a project draft when the requirements are sufficiently clear.
            The project name must be concise and specific (not generic like "Management System").
            The description must reflect real scope, not filler text.

            ## OUTPUT FORMAT

            Return ONLY a valid JSON object — no markdown, no extra text — matching this schema exactly:

            If clarification is needed:
            {
              "ClarificationQuestions": ["<specific question 1>", "<specific question 2>"],
              "NameEn": "",
              "NameAr": "",
              "DescriptionEn": null,
              "DescriptionAr": null
            }

            If the draft is ready:
            {
              "ClarificationQuestions": [],
              "NameEn": "<concise, specific project name in English>",
              "NameAr": "<concise, specific project name in Arabic>",
              "DescriptionEn": "<3-5 sentence description covering purpose, users, and key scope in English>",
              "DescriptionAr": "<3-5 sentence description covering purpose, users, and key scope in Arabic>"
            }
            """;

        public AiProjectGeneratorService(
            IConfiguration configuration,
            IRepository<Project> projectRepo,
            IRepository<Company> companyRepo,
            UserManager<User> userManager)
        {
            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

            _assistantId = configuration["OpenAI:AssistantId"]
                ?? throw new InvalidOperationException(
                    "OpenAI:AssistantId is not configured. " +
                    "Create an assistant at platform.openai.com and set OpenAI:AssistantId in appsettings.");

            _assistantClient = new AssistantClient(apiKey);
            _projectRepo     = projectRepo;
            _companyRepo     = companyRepo;
            _userManager     = userManager;
        }

        // ── Step 1: Generate (OpenAI Thread — no server-side state) ─────────────────

        /// <inheritdoc />
        public async Task<Result<GeneratedProjectDTO>> GenerateProjectAsync(
            string newMessage,
            Guid companyId,
            Guid managerId,
            string? chatId = null)
        {
            if (string.IsNullOrWhiteSpace(newMessage))
                return Result.Failure<GeneratedProjectDTO>(
                    CommonErrors.InvalidInput("Message cannot be empty."));

            //var companyExists = await _companyRepo.AnyAsync(c => c.Id == companyId);
            //if (!companyExists)
            //    return Result.Failure<GeneratedProjectDTO>(CommonErrors.NotFound("Company"));

            var managerError = await ValidateProjectManagerAsync(managerId);
            if (managerError is not null)
                return Result.Failure<GeneratedProjectDTO>(managerError);

            try
            {
                // ── Get or create OpenAI Thread ──────────────────────────────────
                string threadId;

                if (string.IsNullOrEmpty(chatId))
                {
                    // First call — create a new thread on OpenAI's side
                    var thread = await _assistantClient.CreateThreadAsync();
                    threadId = thread.Value.Id;
                }
                else
                {
                    threadId = chatId;
                }

                // ── Add PM's message to the thread ───────────────────────────────
                await _assistantClient.CreateMessageAsync(
                    threadId,
                    MessageRole.User,
                    [MessageContent.FromText(newMessage)]);

                if (string.IsNullOrWhiteSpace(_assistantId))
                {
                    return Result.Failure<GeneratedProjectDTO>(
                        CommonErrors.OperationFailed(
                            "The OpenAI Assistant ID is not configured. Please create an assistant at platform.openai.com and add its ID to 'OpenAI:AssistantId' in appsettings.json."));
                }

                // ── Run the assistant and wait for completion ────────────────────
                var runOptions = new RunCreationOptions
                {
                    // Override the assistant's default instructions each run
                    // so we don't need to configure the assistant itself
                    AdditionalInstructions = SystemPrompt
                };

                var run = await _assistantClient.CreateRunAsync(threadId, _assistantId, runOptions);
                var runId = run.Value.Id;

                // Poll until the run completes (max 60 seconds)
                var deadline = DateTime.UtcNow.AddSeconds(60);
                RunStatus status;
                do
                {
                    await Task.Delay(800);
                    run    = await _assistantClient.GetRunAsync(threadId, runId);
                    status = run.Value.Status;
                }
                while ((status == RunStatus.Queued || status == RunStatus.InProgress)
                       && DateTime.UtcNow < deadline);

                if (status != RunStatus.Completed)
                    return Result.Failure<GeneratedProjectDTO>(
                        CommonErrors.OperationFailed(
                            $"AI run ended with status '{status}'. Please try again."));

                // ── Read the latest assistant message ────────────────────────────
                var messages = _assistantClient.GetMessagesAsync(
                    threadId,
                    new MessageCollectionOptions
                    {
                        Order         = MessageCollectionOrder.Descending,
                        PageSizeLimit = 1
                    });

                var latestMessage = await messages.FirstAsync();
                var rawJson       = CleanJson(latestMessage.Content[0].Text);

                var inner = JsonSerializer.Deserialize<GeneratedProjectDTO>(rawJson, _jsonOptions);

                if (inner is null)
                    return Result.Failure<GeneratedProjectDTO>(
                        CommonErrors.OperationFailed("AI returned an unreadable response. Please try again."));

                // Attach thread ID and entity IDs
                inner.ChatId    = threadId;
                inner.CompanyId = companyId;
                inner.ManagerId = managerId;

                return Result.Success(inner);
            }
            catch (JsonException)
            {
                return Result.Failure<GeneratedProjectDTO>(
                    CommonErrors.OperationFailed("AI response could not be parsed. Please try again."));
            }
            catch (Exception ex)
            {
                return Result.Failure<GeneratedProjectDTO>(
                    CommonErrors.ServerError($"OpenAI call failed: {ex.Message}"));
            }
        }

        // ── Step 2: Confirm (DB write — controller calls SaveChangesAsync) ───────────

        /// <inheritdoc />
        public async Task<Result<Guid>> ConfirmProjectAsync(GeneratedProjectDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NameEn))
                return Result.Failure<Guid>(CommonErrors.InvalidInput("Project name (English) is required."));

            var companyExists = await _companyRepo.AnyAsync(c => c.Id == dto.CompanyId);
            if (!companyExists)
                return Result.Failure<Guid>(CommonErrors.NotFound("Company"));

            var managerError = await ValidateProjectManagerAsync(dto.ManagerId);
            if (managerError is not null)
                return Result.Failure<Guid>(managerError);

            var project = new Project
            {
                NameEn        = dto.NameEn.Trim(),
                NameAr        = dto.NameAr?.Trim() ?? string.Empty,
                DescriptionEn = dto.DescriptionEn?.Trim(),
                DescriptionAr = dto.DescriptionAr?.Trim(),
                CompanyId     = dto.CompanyId,
                ManagerId     = dto.ManagerId
            };

            await _projectRepo.AddAsync(project);

            if (!string.IsNullOrEmpty(dto.ChatId))
            {
                try { await _assistantClient.DeleteThreadAsync(dto.ChatId); }
                catch { /* best-effort cleanup — don't fail the confirmation */ }
            }

            return Result.Success(project.Id);
        }

        // ── Private helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Looks up the user in AspNetUsers (TPH) and confirms they hold the ProjectManager role.
        /// Returns null on success, or an <see cref="Error"/> on failure.
        /// </summary>
        private async Task<Error?> ValidateProjectManagerAsync(Guid managerId)
        {
            var user = await _userManager.FindByIdAsync(managerId.ToString());

            if (user is null)
                return CommonErrors.NotFound("Project Manager");

            var isManager = await _userManager.IsInRoleAsync(user, "ProjectManager");

            if (!isManager)
                return CommonErrors.Forbidden("The specified user is not a Project Manager.");

            return null;
        }

        private static string CleanJson(string input) =>
            input.Replace("```json", "")
                 .Replace("```", "")
                 .Trim();
    }
}
