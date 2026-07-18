using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Enums;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Helpers;
namespace TaskPilot.AI.Agents.Requirements
{
    public class RequirementAnalysisAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly IVectorStore _vectorStore;
        private readonly IDocumentStore _documentStore;

        public RequirementAnalysisAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            IVectorStore vectorStore,
            IDocumentStore documentStore)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _vectorStore = vectorStore;
            _documentStore = documentStore;
        }

        public async Task<RequirementAnalysisResult> AnalyzeAsync(
            RequirementSession session,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<TaskPilot.AI.Models.Ingestion.KnowledgeChunk>();
            foreach (var doc in session.Knowledge.Documents)
            {
                var docChunks = await _documentStore.GetChunksAsync(doc.Id, cancellationToken);
                chunks.AddRange(docChunks);
            }

            // Removed early fallback for 1-line history so the LLM can dynamically generate questions

            var documentContent = string.Join("\n\n---\n\n",
                chunks.OrderBy(c => c.ChunkIndex).Select((c, i) => $"[Section {i + 1}]\n{c.Content}"));

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/RequirementAnalysis.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            var conversationHistoryJson = JsonSerializer.Serialize(session.ConversationHistory, jsonOptions);
            var existingConfidenceScoresJson = JsonSerializer.Serialize(session.ConfidenceScores, jsonOptions);
            var existingQuestionsJson = JsonSerializer.Serialize(session.QuestionPool, jsonOptions);

            // Use deterministic settings (Temperature=0.1) for the scoring/analysis call
            // to reduce LLM variance in the confidence scores and gap questions.
            // GenerateInterviewQuestionsAsync deliberately keeps its default settings (conversational).
            var analysisArguments = KernelArgumentsFactory.CreateDeterministicArguments();
            analysisArguments["documentContent"] = documentContent;
            analysisArguments["conversationHistory"] = conversationHistoryJson;
            analysisArguments["existingConfidenceScores"] = existingConfidenceScoresJson;
            analysisArguments["existingQuestions"] = existingQuestionsJson;

            var invokeResult = await kernel.InvokeAsync(
                function,
                analysisArguments,
                cancellationToken: cancellationToken);

            var raw = invokeResult.ToString().Trim();
            if (raw.StartsWith("```"))
                raw = raw.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                return JsonSerializer.Deserialize<RequirementAnalysisResult>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? GetFallbackResult();
            }
            catch (JsonException)
            {
                return GetFallbackResult();
            }
        }

        public async Task<InterviewQuestionGenerationResult> GenerateInterviewQuestionsAsync(
            RequirementSession session,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/InterviewQuestionGeneration.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            var conversationHistoryJson = JsonSerializer.Serialize(session.ConversationHistory, jsonOptions);

            var invokeResult = await kernel.InvokeAsync(
                function,
                new KernelArguments 
                { 
                    ["initialContext"] = conversationHistoryJson
                },
                cancellationToken: cancellationToken);

            var raw = invokeResult.ToString().Trim();
            if (raw.StartsWith("```"))
                raw = raw.Replace("```json", "").Replace("```", "").Trim();

            try
            {
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

                return JsonSerializer.Deserialize<InterviewQuestionGenerationResult>(
                    raw,
                    options)
                    ?? new InterviewQuestionGenerationResult();
            }
            catch (JsonException)
            {
                return new InterviewQuestionGenerationResult();
            }
        }

        private static RequirementAnalysisResult GetFallbackResult() => new()
        {
            ExtractedRequirements = new ExtractedRequirements(),
            ConfidenceScores = new List<CategoryConfidence>
            {
                new() { Category = "BusinessGoals", Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "Primary objectives", "Success criteria", "KPIs" } },
                new() { Category = "Scale",         Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "Concurrent users", "Expected load", "Data volume" } },
                new() { Category = "Integration",   Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "Third-party APIs", "External systems" } },
                new() { Category = "Timeline",      Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "Go-live date", "Milestones", "Project phases" } },
                new() { Category = "Compliance",    Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "Regulatory standards", "Security requirements", "Data retention" } },
                new() { Category = "UserRoles",     Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "User types", "Access levels", "Permissions" } },
                new() { Category = "Realtime",      Score = 0, Status = "Missing",
                        Reason   = "No document content was available to analyse.",
                        Evidence = string.Empty,
                        MissingItems = new List<string> { "Live update requirements", "Notification strategy" } },
            },
            GapQuestions = new List<GapQuestion>(),
            LlmEstimatedCompletenessScore = 0,
            FinalizeReadiness        = false,
            EstimatedReadiness       = 0,
            Recommendation           = "No document content was retrieved from the vector store. " +
                                       "Please ensure the BRD was uploaded and indexed successfully, " +
                                       "then answer the gap questions above before finalising.",
            RequirementCompletenessReport = new RequirementCompletenessReport
            {
                OverallCompleteness = 0,
                Readiness = "Needs Clarification",
                ReadinessRecommendation = "No document content was available.",
                ReadyForFinalization = false
            }
        };
    }
}
