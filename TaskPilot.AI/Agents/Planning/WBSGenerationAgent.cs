using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Models.Entities;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Persistence.Interfaces;

namespace TaskPilot.AI.Agents.Planning
{
    public class WBSGenerationAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly IVectorStore _vectorStore;
        private readonly IDocumentStore _documentStore;
        private readonly IRequirementSessionStore _sessionStore;
        private readonly Microsoft.Extensions.Logging.ILogger<WBSGenerationAgent> _logger;
        private readonly ITelemetryAccumulator _telemetry;

        public WBSGenerationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            IVectorStore vectorStore,
            IDocumentStore documentStore,
            IRequirementSessionStore sessionStore,
            Microsoft.Extensions.Logging.ILogger<WBSGenerationAgent> logger,
            ITelemetryAccumulator telemetry)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _vectorStore = vectorStore;
            _documentStore = documentStore;
            _sessionStore = sessionStore;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task<GeneratedWbs> GenerateAsync(
            RequirementsSnapshot snapshot,
            System.Collections.Generic.List<string> techStack,
            System.Collections.Generic.List<string> platformTargets,
            string projectType,
            System.Collections.Generic.List<string> availableSkills,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var brdContext = string.Empty;

            if (sessionId != Guid.Empty)
            {
                try
                {
                    var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
                    if (session != null && session.Knowledge?.Documents != null && session.Knowledge.Documents.Any())
                    {
                        var chunks = new System.Collections.Generic.List<TaskPilot.AI.Models.Ingestion.KnowledgeChunk>();
                        foreach (var doc in session.Knowledge.Documents)
                        {
                            var docChunks = await _documentStore.GetChunksAsync(doc.Id, cancellationToken);
                            chunks.AddRange(docChunks);
                        }

                        const int MaxContextTokens = 100000;
                        int totalTokens = TaskPilot.AI.Helpers.TokenHelper.EstimateTokens(string.Join(" ", chunks.Select(c => c.Content)));
                        
                        _logger.LogInformation("WBSGenerationAgent: Total tokens estimated for session {SessionId} is {TotalTokens}. MaxContextTokens={MaxContextTokens}", sessionId, totalTokens, MaxContextTokens);

                        if (totalTokens > MaxContextTokens)
                        {
                            _logger.LogInformation("WBSGenerationAgent: Token limit exceeded. Falling back to RAG.");
                            var dynamicQuery = string.Join(" ", session.Knowledge.Documents.Select(d => $"{d.Category} {d.FileName}"));
                            var queryText = $"business requirements features user stories functional requirements processes workflows modules system capabilities for {dynamicQuery}";

                            var relevantChunks = await _vectorStore.SearchAsync(
                                collectionType: TaskPilot.Models.Enums.KnowledgeCollectionType.ProjectPolicies,
                                requirementSessionId: sessionId,
                                projectId: null,
                                companyId: null,
                                queryText: queryText,
                                topK: 25,
                                categoryFilter: null,
                                cancellationToken: cancellationToken
                            );

                            chunks = relevantChunks.ToList();

                            while (chunks.Count > 0 && TaskPilot.AI.Helpers.TokenHelper.EstimateTokens(string.Join(" ", chunks.Select(c => c.Content))) > MaxContextTokens)
                            {
                                chunks.RemoveAt(chunks.Count - 1);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("WBSGenerationAgent: Full context path taken (No RAG fallback).");
                        }

                        if (chunks.Any())
                        {
                            _logger.LogInformation("WBSGenerationAgent: Feeding {ChunkCount} chunks into final WBS prompt.", chunks.Count);
                            var chunksList = chunks.OrderBy(c => c.SourceFile).ThenBy(c => c.ChunkIndex).ToList();
                            brdContext = string.Join("\n\n---\n\n",
                                chunksList.Select((chunk, i) =>
                                    $"[Document Excerpt {i + 1}]\n{chunk.Content}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Document retrieval failed for session {SessionId}. Proceeding without BRD context.", sessionId);
                    brdContext = string.Empty;
                }
            }

            var kernel = _kernelService.CreateKernel(
                ModelConstants.MorePowerfulModel,
                "LongRunningAiClient");

            var prompt = await _promptLoader.LoadAsync(
                "Planning/WbsGeneration.yaml");

            prompt += "\n" +
                      "  CRITICAL: Your response must be a single complete valid JSON object with no trailing text.\n" +
                      "  Do not truncate or cut off the response under any circumstances.\n";

            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object> { 
                    { "max_tokens", 16384 },
                    { "temperature", 0.0 },
                    { "seed", 42 }
                }
            };

            var arguments = new KernelArguments(executionSettings)
            {
                ["businessRequirements"] =
                    string.Join("\n", snapshot.BusinessRequirements),

                ["technicalRequirements"] =
                    string.Join("\n", snapshot.TechnicalRequirements),

                ["constraints"] =
                    string.Join("\n", snapshot.Constraints),

                ["integrations"] =
                    string.Join("\n", snapshot.Integrations),

                ["scaleRequirements"] =
                    string.Join("\n", snapshot.ScaleRequirements),

                ["techStack"] =
                    techStack != null && techStack.Any()
                        ? string.Join(", ", techStack)
                        : "Not specified — use best practices",

                ["platformTargets"] =
                    platformTargets != null && platformTargets.Any()
                        ? string.Join(", ", platformTargets)
                        : "Web",

                ["projectType"] =
                    !string.IsNullOrEmpty(projectType)
                        ? projectType
                        : "General",

                ["availableSkills"] =
                    availableSkills != null && availableSkills.Any()
                        ? string.Join(", ", availableSkills)
                        : "None",
                        
                ["brdContext"] = brdContext
            };

            const int maxAttempts = 3;
            GeneratedWbs? result = null;
            Exception? lastException = null;
            string rawJson = string.Empty;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    string currentPrompt = prompt;

                    var function = KernelFunctionYaml.FromPromptYaml(currentPrompt);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var invokeResult = await kernel.InvokeAsync(
                        function,
                        arguments,
                        cancellationToken: cancellationToken);
                    sw.Stop();

                    _telemetry.RecordCall(invokeResult.Metadata, sw.ElapsedMilliseconds, "WBSGenerationAgent", ModelConstants.MorePowerfulModel, _logger);

                    rawJson = invokeResult.ToString();
                    
                    int approxTokens = rawJson.Length / 4;
                    _logger.LogInformation("WBS Generation Attempt {Attempt}: Generated Response Length {Length} characters, approx {Tokens} tokens.", attempt, rawJson.Length, approxTokens);

                    string jsonToParse = attempt == 1 ? TryRepairJson(rawJson) : TryRepairJson(rawJson);
                    result = JsonSerializer.Deserialize<GeneratedWbs>(jsonToParse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (result != null) break;
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                    string retryReason = attempt < maxAttempts ? "Reducing size for next attempt" : "Max attempts reached";
                    _logger.LogWarning("WBS JSON parse failed on attempt {Attempt}/3. Reason: {Message}. Retry reason: {RetryReason}. Response length: {Length}", attempt, ex.Message, retryReason, rawJson?.Length ?? 0);
                }
            }

            if (result == null || !result.UserStories.Any())
                throw new WbsGenerationException(
                    $"WBS generation failed after {maxAttempts} attempts due to truncated or invalid JSON. " +
                    "Try reducing the number of requirements or contact support.",
                    lastException?.ToString());

            return result;
        }

        private string TryRepairJson(string raw)
        {
            raw = raw.Trim();
            if (!raw.StartsWith("{")) return raw; // not repairable, return as-is
            if (raw.EndsWith("}")) return raw;    // already complete

            // Find the last fully closed userStory object
            // A closed userStory ends with: "}" followed optionally by whitespace then "," or "]"
            int lastClosingBrace = -1;
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 1) // closed a userStory (depth 1 = inside root object)
                        lastClosingBrace = i;
                    else if (depth == 0)
                        return raw; // already balanced
                }
            }

            if (lastClosingBrace == -1) return raw; // cannot repair

            // Truncate after last complete userStory and close the JSON
            string repaired = raw.Substring(0, lastClosingBrace + 1) + "\n  ]\n}";
            return repaired;
        }
    }
}
