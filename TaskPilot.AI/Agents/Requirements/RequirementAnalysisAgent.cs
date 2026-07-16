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

            // Fallback for sessions with no chunks
            if (!chunks.Any())
                return GetFallbackResult();

            var documentContent = string.Join("\n\n---\n\n",
                chunks.OrderBy(c => c.ChunkIndex).Select((c, i) => $"[Section {i + 1}]\n{c.Content}"));

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/RequirementAnalysis.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            var conversationHistoryJson = JsonSerializer.Serialize(session.ConversationHistory, jsonOptions);
            var existingConfidenceScoresJson = JsonSerializer.Serialize(session.ConfidenceScores, jsonOptions);
            var existingQuestionsJson = JsonSerializer.Serialize(session.QuestionPool, jsonOptions);

            var invokeResult = await kernel.InvokeAsync(
                function,
                new KernelArguments 
                { 
                    ["documentContent"] = documentContent,
                    ["conversationHistory"] = conversationHistoryJson,
                    ["existingConfidenceScores"] = existingConfidenceScoresJson,
                    ["existingQuestions"] = existingQuestionsJson
                },
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
            GapQuestions = new List<GapQuestion>
            {
                new() { Question = "What are the primary business goals for this project?",
                        Category = "BusinessGoals", Priority = "Critical",
                        Reason   = "No document was available; business goals are required to start analysis.",
                        MissingItems = new List<string> { "Primary objectives", "Success criteria" },
                        BusinessImpact = "Without business goals, no meaningful requirements can be derived.",
                        EstimatedEffectOnCompleteness = 25 },
                new() { Question = "What is the expected number of concurrent users and overall system scale?",
                        Category = "Scale", Priority = "High",
                        Reason   = "Scale is required for infrastructure sizing and capacity planning.",
                        MissingItems = new List<string> { "Concurrent users", "Expected load" },
                        BusinessImpact = "Without scale requirements, infrastructure cannot be designed.",
                        EstimatedEffectOnCompleteness = 12 },
                new() { Question = "What third-party integrations or external systems are required?",
                        Category = "Integration", Priority = "High",
                        Reason   = "Integration scope directly affects technical architecture and timeline.",
                        MissingItems = new List<string> { "Third-party APIs", "External systems" },
                        BusinessImpact = "Unknown integrations may cause scope creep and delivery delays.",
                        EstimatedEffectOnCompleteness = 10 },
                new() { Question = "What is the project timeline, including key milestones and go-live date?",
                        Category = "Timeline", Priority = "High",
                        Reason   = "Milestones are needed for sprint planning and WBS generation.",
                        MissingItems = new List<string> { "Go-live date", "Milestones", "Project phases" },
                        BusinessImpact = "Without a timeline, sprint commitments cannot be established.",
                        EstimatedEffectOnCompleteness = 12 },
                new() { Question = "Who are the different user roles and what are their access levels?",
                        Category = "UserRoles", Priority = "High",
                        Reason   = "User roles define functional scope and security boundaries.",
                        MissingItems = new List<string> { "User types", "Access levels", "Permissions" },
                        BusinessImpact = "Without user roles, feature scope and authorization design are undefined.",
                        EstimatedEffectOnCompleteness = 15 },
            },
            OverallCompletenessScore = 0,
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
