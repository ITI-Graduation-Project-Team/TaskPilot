using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class RequirementsBuilderAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public RequirementsBuilderAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<StructuredRequirements>
            BuildAsync(
                RequirementSession session,
                CancellationToken cancellationToken = default)
        {
            var kernel =
                _kernelService
                    .CreateKernel(
                        ModelConstants
                            .PowerfulModel);

            var prompt =
                await _promptLoader
                    .LoadAsync(
                        "Requirements/Builder.yaml");

            var function =
                KernelFunctionYaml
                    .FromPromptYaml(
                        prompt);

            var conversationText = string.Join("\n", session
                .ConversationHistory
                .Select(m => $"[{m.Timestamp:yyyy-MM-dd HH:mm}] {m.Role}: {m.Message}"));

            var answeredQuestions = string.Join("\n", session
                .QuestionPool
                .Where(q => q.IsAnswered && !string.IsNullOrWhiteSpace(q.Answer))
                .Select(q =>
                    $"Category: {q.Category}\n" +
                    $"Q: {q.Question}\n" +
                    $"A: {q.Answer}\n" +
                    $"Source: {q.AnsweredFromSource ?? "PM"}\n" +
                    $"AnsweredAt: {q.AnsweredAt:yyyy-MM-dd HH:mm}"));

            var documentContext = string.Empty;

            if (session.Knowledge?.Documents != null
                && session.Knowledge.Documents.Any())
            {
                var documentTexts = session.Knowledge.Documents
                    .Where(d => !string.IsNullOrWhiteSpace(d.ExtractedText))
                    .Select(d =>
                        $"[Document: {d.FileName} | " +
                        $"Category: {d.Category} | " +
                        $"Uploaded: {d.UploadedAt:yyyy-MM-dd HH:mm}]\n" +
                        $"{d.ExtractedText}");

                documentContext = string.Join("\n\n", documentTexts);
            }

            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["conversationHistory"] = conversationText,
                    ["answeredQuestions"]   = answeredQuestions,
                    ["documentContext"]     = documentContext
                },
                cancellationToken: cancellationToken);

            var json =
                result.ToString()
                      .Trim();

            var structuredRequirements =
                JsonSerializer.Deserialize
                    <StructuredRequirements>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive =
                                true
                        });

            if (structuredRequirements
                is null)
            {
                throw new Exception(
                    "Failed to build structured requirements.");
            }

            // Save decision using extension
            session.AddDecision(
                nameof(RequirementsBuilderAgent),
                "Structured requirements document generated");

            return structuredRequirements;
        }
    }
}
