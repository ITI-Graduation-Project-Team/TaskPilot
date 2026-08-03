using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Workflow;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.Models.Enums;

namespace TaskPilot.AI.Agents.Requirements
{
    public class RequirementsBuilderAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;
        private readonly IVectorStore _vectorStore;
        private readonly Microsoft.Extensions.Logging.ILogger<RequirementsBuilderAgent> _logger;
        private readonly ITelemetryAccumulator _telemetry;

        public RequirementsBuilderAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            IVectorStore vectorStore,
            Microsoft.Extensions.Logging.ILogger<RequirementsBuilderAgent> logger,
            ITelemetryAccumulator telemetry)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
            
            _vectorStore = vectorStore;
            _logger = logger;
            _telemetry = telemetry;
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
                .Select((m, i) => $"[Message {i + 1}] {m.Role}: {m.Message}"));

            var answeredQuestions = string.Join("\n", session
                .QuestionPool
                .Where(q => q.IsAnswered && !string.IsNullOrWhiteSpace(q.Answer))
                .Select(q =>
                    $"Category: {q.Category}\n" +
                    $"Q: {q.Question}\n" +
                    $"A: {q.Answer}\n" +
                    $"Source: {q.AnsweredFromSource ?? "PM"}"));

            var documentContext = string.Empty;

            if (session.Knowledge?.Documents != null
                && session.Knowledge.Documents.Any())
            {
                const int MaxContextTokens = 100000;
                
                var documentTexts = session.Knowledge.Documents
                    .Where(d => !string.IsNullOrWhiteSpace(d.ExtractedText))
                    .Select(d =>
                        $"[Document: {d.FileName} | " +
                        $"Category: {d.Category}]\n" +
                        $"{d.ExtractedText}");

                documentContext = string.Join("\n\n", documentTexts);

                if (TokenHelper.EstimateTokens(documentContext) > MaxContextTokens)
                {
                    var dynamicQuery = string.Join(" ", session.Knowledge.Documents.Select(d => $"{d.Category} {d.FileName}"));
                    var queryText = $"Functional requirements, non-functional requirements, technical integrations, business rules, system constraints, entities, relationships, and diagram content for {dynamicQuery}";

                    var relevantChunks = await _vectorStore.SearchAsync(
                        KnowledgeCollectionType.ProjectPolicies,
                        requirementSessionId: session.SessionId,
                        projectId: session.ProjectId,
                        companyId: null, // Scoped to session/project only
                        queryText: queryText,
                        topK: 25,
                        cancellationToken: cancellationToken);

                    // Stabilize ties from Qdrant by ordering by SourceFile and ChunkIndex
                    var chunksList = relevantChunks.OrderBy(c => c.SourceFile).ThenBy(c => c.ChunkIndex).ToList();
                    while (chunksList.Count > 0 && TokenHelper.EstimateTokens(string.Join(" ", chunksList.Select(c => c.Content))) > MaxContextTokens)
                    {
                        chunksList.RemoveAt(chunksList.Count - 1);
                    }

                    // Fully deterministic ordering for final text
                    documentContext = string.Join("\n\n---\n\n", chunksList.OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex).Select((c, i) => $"[Section {i + 1}]\n{c.Content}"));
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["conversationHistory"] = conversationText,
                    ["answeredQuestions"]   = answeredQuestions,
                    ["documentContext"]     = documentContext
                },
                cancellationToken: cancellationToken);
            sw.Stop();

            _telemetry.RecordCall(result.Metadata, sw.ElapsedMilliseconds, "RequirementsBuilderAgent", ModelConstants.PowerfulModel, _logger);

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
