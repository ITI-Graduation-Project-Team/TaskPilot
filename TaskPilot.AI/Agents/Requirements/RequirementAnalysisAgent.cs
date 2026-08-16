using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
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
        private readonly Microsoft.Extensions.Logging.ILogger<RequirementAnalysisAgent> _logger;
        private readonly ITelemetryAccumulator _telemetry;
        private readonly ITokenQuotaEnforcer _tokenQuotaEnforcer;

        public RequirementAnalysisAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            IVectorStore vectorStore,
            IDocumentStore documentStore,
            Microsoft.Extensions.Logging.ILogger<RequirementAnalysisAgent> logger,
            ITelemetryAccumulator telemetry,
            ITokenQuotaEnforcer tokenQuotaEnforcer)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _vectorStore = vectorStore;
            _documentStore = documentStore;
            _logger = logger;
            _telemetry = telemetry;
            _tokenQuotaEnforcer = tokenQuotaEnforcer;
        }

        public async Task<RequirementAnalysisResult> AnalyzeAsync(
            RequirementSession session,
            CancellationToken cancellationToken = default,
            string lang = "en")
        {
            var chunks = new List<TaskPilot.AI.Models.Ingestion.KnowledgeChunk>();
            foreach (var doc in session.Knowledge.Documents)
            {
                var docChunks = await _documentStore.GetChunksAsync(doc.Id, cancellationToken);
                chunks.AddRange(docChunks);
            }

            const int MaxContextTokens = 100000;
            int totalTokens = TokenHelper.EstimateTokens(string.Join(" ", chunks.Select(c => c.Content)));
            if (totalTokens > MaxContextTokens)
            {
                var dynamicQuery = string.Join(" ", session.Knowledge.Documents.Select(d => $"{d.Category} {d.FileName}"));
                var queryText = $"Core business objectives, requirements, constraints, entities, data models, relationships, and diagrams for {dynamicQuery}";

                var relevantChunks = await _vectorStore.SearchAsync(
                    KnowledgeCollectionType.ProjectPolicies,
                    requirementSessionId: session.SessionId,
                    projectId: session.ProjectId,
                    companyId: null, // Scoped to session/project only
                    queryText: queryText,
                    topK: 25,
                    cancellationToken: cancellationToken);

                // Stabilize ties from Qdrant by ordering by SourceFile and ChunkIndex before truncation
                chunks = relevantChunks.OrderBy(c => c.SourceFile).ThenBy(c => c.ChunkIndex).ToList();

                while (chunks.Count > 0 && TokenHelper.EstimateTokens(string.Join(" ", chunks.Select(c => c.Content))) > MaxContextTokens)
                {
                    chunks.RemoveAt(chunks.Count - 1);
                }
            }

            // Removed early fallback for 1-line history so the LLM can dynamically generate questions

            // Ensure fully deterministic ordering for identical documents
            chunks = chunks.OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex).ToList();

            var documentContent = string.Join("\n\n---\n\n",
                chunks.Select((c, i) => $"[Section {i + 1}]\n{c.Content}"));

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/RequirementAnalysis.yaml");

            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            var conversationHistoryJson = JsonSerializer.Serialize(session.ConversationHistory, jsonOptions);
            var existingConfidenceScoresJson = JsonSerializer.Serialize(session.ConfidenceScores, jsonOptions);
            var existingQuestionsJson = JsonSerializer.Serialize(session.QuestionPool, jsonOptions);

            int templateIndex = prompt.IndexOf("template: |");
            var yamlPromptBody = templateIndex >= 0 
                ? prompt.Substring(templateIndex + 11).Trim() 
                : prompt;

            yamlPromptBody = yamlPromptBody
                .Replace("{{$documentContent}}", documentContent)
                .Replace("{{$conversationHistory}}", conversationHistoryJson)
                .Replace("{{$existingConfidenceScores}}", existingConfidenceScoresJson)
                .Replace("{{$existingQuestions}}", existingQuestionsJson);

            var systemMessage = lang == "ar"
                ? $"""
                   أنت مساعد متخصص في تحليل متطلبات المشاريع.
                   يجب أن تكون جميع ردودك وجميع حقول JSON باللغة العربية الفصحى حصراً.
                   لا تستخدم الإنجليزية في أي جزء من الإخراج.
                   لا تضف أي جملة تمهيدية. أخرج JSON فقط بدون أي نص إضافي.
                   لا تكتب أي نص قبل أو بعد JSON. يجب أن يكون ردك كاملاً عبارة عن كائن JSON واحد صالح. بدون مقدمة أو تفسير أو ملاحظات ختامية.

                   {yamlPromptBody}
                   """
                : $"""
                   You are a project requirements analysis assistant.
                   Respond in English only. Output raw JSON only, no preamble.
                   Do NOT output any text before or after the JSON. Your entire response must be a single valid JSON object. No introduction, no explanation, no closing remarks.

                   {yamlPromptBody}
                   """;

            var chat = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chat.AddSystemMessage(systemMessage);
            chat.AddUserMessage(conversationHistoryJson);

            var chatCompletionService = kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            
            var settings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                ResponseFormat = "json_object",
                Temperature = 0.0,
                Seed = 42
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await chatCompletionService.GetChatMessageContentAsync(chat, settings, kernel, cancellationToken);
            sw.Stop();

            _telemetry.RecordCall(response.Metadata, sw.ElapsedMilliseconds, "RequirementAnalysisAgent", ModelConstants.PowerfulModel, _logger);

            var raw = response.Content?.Trim() ?? string.Empty;
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
            CancellationToken cancellationToken = default,
            string lang = "en")
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/InterviewQuestionGeneration.yaml");
            
            var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
            var conversationHistoryJson = JsonSerializer.Serialize(session.ConversationHistory, jsonOptions);

            int templateIndex = prompt.IndexOf("template: |");
            var yamlPromptBody = templateIndex >= 0 
                ? prompt.Substring(templateIndex + 11).Trim() 
                : prompt;

            yamlPromptBody = yamlPromptBody
                .Replace("{{$initialContext}}", conversationHistoryJson);

            var systemMessage = lang == "ar"
                ? $"""
                   أنت مساعد متخصص في تحليل متطلبات المشاريع.
                   يجب أن تكون جميع ردودك وجميع حقول JSON باللغة العربية الفصحى حصراً.
                   لا تستخدم الإنجليزية في أي جزء من الإخراج.
                   لا تضف أي جملة تمهيدية. أخرج JSON فقط بدون أي نص إضافي.
                   لا تكتب أي نص قبل أو بعد JSON. يجب أن يكون ردك كاملاً عبارة عن كائن JSON واحد صالح. بدون مقدمة أو تفسير أو ملاحظات ختامية.

                   {yamlPromptBody}
                   """
                : $"""
                   You are a project requirements analysis assistant.
                   Respond in English only. Output raw JSON only, no preamble.
                   Do NOT output any text before or after the JSON. Your entire response must be a single valid JSON object. No introduction, no explanation, no closing remarks.

                   {yamlPromptBody}
                   """;

            var chat = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chat.AddSystemMessage(systemMessage);
            chat.AddUserMessage(conversationHistoryJson);

            var chatCompletionService = kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            
            var settings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                ResponseFormat = "json_object"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await chatCompletionService.GetChatMessageContentAsync(chat, settings, kernel, cancellationToken);
            sw.Stop();

            _telemetry.RecordCall(response.Metadata, sw.ElapsedMilliseconds, "RequirementAnalysisAgent", ModelConstants.PowerfulModel, _logger);

            var raw = response.Content?.Trim() ?? string.Empty;
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
