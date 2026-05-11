using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.Text.Json;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.AiProjectGenerator
{
    /// <summary>
    /// Generates a project draft from natural-language requirements via OpenAI GPT
    /// and persists the PM-approved draft as a <see cref="Project"/> entity.
    ///
    /// Two-step flow:
    ///   1. <see cref="GenerateProjectAsync"/> — calls AI, returns draft to PM (no DB write).
    ///   2. <see cref="ConfirmProjectAsync"/> — persists the approved draft (controller calls SaveChangesAsync).
    /// </summary>
    public class AiProjectGeneratorService : IAiProjectGeneratorService
    {
        private readonly ChatClient _chatClient;
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<Company> _companyRepo;
        private readonly IRepository<ProjectManager> _managerRepo;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public AiProjectGeneratorService(
            IConfiguration configuration,
            IRepository<Project> projectRepo,
            IRepository<Company> companyRepo,
            IRepository<ProjectManager> managerRepo)
        {
            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

            _chatClient = new ChatClient(model: "gpt-4o-mini", apiKey: apiKey);
            _projectRepo = projectRepo;
            _companyRepo = companyRepo;
            _managerRepo = managerRepo;
        }

        // ── Step 1: Generate (no DB write) ──────────────────────────────────────────

        /// <inheritdoc />
        public async Task<Result<GeneratedProjectDTO>> GenerateProjectAsync(
            string requirements,
            Guid companyId,
            Guid managerId)
        {
            if (string.IsNullOrWhiteSpace(requirements))
                return Result.Failure<GeneratedProjectDTO>(
                    CommonErrors.InvalidInput("Project requirements cannot be empty."));

            var companyExists = await _companyRepo.AnyAsync(c => c.Id == companyId);
            if (!companyExists)
                return Result.Failure<GeneratedProjectDTO>(CommonErrors.NotFound("Company"));

            var managerExists = await _managerRepo.AnyAsync(pm => pm.Id == managerId);
            if (!managerExists)
                return Result.Failure<GeneratedProjectDTO>(CommonErrors.NotFound("Project Manager"));

            var draft = await CallOpenAiAsync(requirements, companyId, managerId);
            return draft;
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

            var managerExists = await _managerRepo.AnyAsync(pm => pm.Id == dto.ManagerId);
            if (!managerExists)
                return Result.Failure<Guid>(CommonErrors.NotFound("Project Manager"));

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

            return Result.Success(project.Id);
        }

        // ── Private helpers ──────────────────────────────────────────────────────────

        private async Task<Result<GeneratedProjectDTO>> CallOpenAiAsync(
            string requirements,
            Guid companyId,
            Guid managerId)
        {
            var systemPrompt = """
                You are a professional project manager assistant.
                Your job is to generate a concise project definition from the user's requirements.

                Return ONLY a valid JSON object — no markdown, no extra text — matching exactly this schema:
                {
                  "NameEn": "<project name in English>",
                  "NameAr": "<project name in Arabic>",
                  "DescriptionEn": "<2-4 sentence project description in English>",
                  "DescriptionAr": "<2-4 sentence project description in Arabic>"
                }
                """;

            var userPrompt = $"Project requirements:\n{requirements}";

            try
            {
                var response = await _chatClient.CompleteChatAsync(
                    new ChatMessage[]
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt)
                    });

                var rawJson = CleanJson(response.Value.Content[0].Text);

                var inner = JsonSerializer.Deserialize<GeneratedProjectDTO>(rawJson, _jsonOptions);

                if (inner is null)
                    return Result.Failure<GeneratedProjectDTO>(
                        CommonErrors.OperationFailed("AI returned an unreadable response. Please try again."));

                // Attach the IDs that were echoed from the request
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

        private static string CleanJson(string input) =>
            input.Replace("```json", "")
                 .Replace("```", "")
                 .Trim();
    }
}
