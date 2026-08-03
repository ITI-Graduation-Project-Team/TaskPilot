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
            var brdContext = await GetBrdContextAsync(sessionId, cancellationToken);

            var kernel = _kernelService.CreateKernel(
                ModelConstants.MorePowerfulModel,
                "LongRunningAiClient");

            // Phase 1: Generate Stories
            var stories = await GenerateStoriesAsync(kernel, snapshot, techStack, platformTargets, projectType, availableSkills, brdContext, cancellationToken);
            
            // Phase 2: Generate Tasks in Batches
            var storyTasks = await GenerateTasksBatchedAsync(kernel, stories, techStack, availableSkills, cancellationToken);

            // Merge Tasks into Stories
            foreach (var story in stories)
            {
                var generatedTasks = storyTasks.FirstOrDefault(st => st.StoryId == story.Id)?.Tasks;
                if (generatedTasks != null)
                {
                    story.Tasks = generatedTasks;
                }
            }

            return new GeneratedWbs { UserStories = stories };
        }

        private async Task<string> GetBrdContextAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            if (sessionId == Guid.Empty) return string.Empty;

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
                        return string.Join("\n\n---\n\n",
                            chunksList.Select((chunk, i) =>
                                $"[Document Excerpt {i + 1}]\n{chunk.Content}"));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document retrieval failed for session {SessionId}. Proceeding without BRD context.", sessionId);
            }
            
            return string.Empty;
        }

        private async Task<List<GeneratedUserStory>> GenerateStoriesAsync(
            Kernel kernel,
            RequirementsSnapshot snapshot,
            List<string> techStack,
            List<string> platformTargets,
            string projectType,
            List<string> availableSkills,
            string brdContext,
            CancellationToken cancellationToken)
        {
            var prompt = await _promptLoader.LoadAsync("Planning/WbsStoryGeneration.yaml");
            
            prompt += "\n" +
                      "  CRITICAL: Your response must be a single complete valid JSON object with no trailing text.\n" +
                      "  Do not truncate or cut off the response under any circumstances.\n";

            var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                MaxTokens = 32768,
                Temperature = 0.0,
                Seed = 42,
                ResponseFormat = "json_object"
            };

            _logger.LogInformation("WBSGenerationAgent: Executing Call 1 with MaxTokens={MaxTokens}, ResponseFormat={ResponseFormat}", executionSettings.MaxTokens, executionSettings.ResponseFormat);

            var arguments = new KernelArguments(executionSettings)
            {
                ["businessRequirements"] = string.Join("\n", snapshot.BusinessRequirements),
                ["technicalRequirements"] = string.Join("\n", snapshot.TechnicalRequirements),
                ["constraints"] = string.Join("\n", snapshot.Constraints),
                ["integrations"] = string.Join("\n", snapshot.Integrations),
                ["scaleRequirements"] = string.Join("\n", snapshot.ScaleRequirements),
                ["techStack"] = techStack != null && techStack.Any() ? string.Join(", ", techStack) : "Not specified — use best practices",
                ["platformTargets"] = platformTargets != null && platformTargets.Any() ? string.Join(", ", platformTargets) : "Web",
                ["projectType"] = !string.IsNullOrEmpty(projectType) ? projectType : "General",
                ["availableSkills"] = availableSkills != null && availableSkills.Any() ? string.Join(", ", availableSkills) : "None",
                ["brdContext"] = brdContext
            };

            const int maxAttempts = 3;
            GeneratedWbs? result = null;
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                FunctionResult? invokeResult = null;
                try
                {
                    var function = KernelFunctionYaml.FromPromptYaml(prompt);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    invokeResult = await kernel.InvokeAsync(function, arguments, cancellationToken);
                    sw.Stop();

                    _telemetry.RecordCall(invokeResult.Metadata, sw.ElapsedMilliseconds, "WBSGenerationAgent_Stories", ModelConstants.MorePowerfulModel, _logger);
                    
                    if (invokeResult.Metadata != null && invokeResult.Metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
                    {
                        _logger.LogInformation("WBS Story Generation Attempt {Attempt} Tokens: {Usage}", attempt, usageObj.ToString());
                    }

                    var rawJson = invokeResult.ToString();
                    var jsonToParse = TryRepairJson(rawJson);
                    result = JsonSerializer.Deserialize<GeneratedWbs>(jsonToParse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (result != null && result.UserStories.Any()) break;
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                    _logger.LogWarning("WBS Story JSON parse failed on attempt {Attempt}/3. Exception: {Message}. Raw JSON (first 500 chars): {RawJson}", attempt, ex.Message, invokeResult?.ToString()?.Substring(0, Math.Min(500, invokeResult?.ToString()?.Length ?? 0)));
                }
            }

            if (result == null || !result.UserStories.Any())
                throw new WbsGenerationException("WBS story generation failed after 3 attempts due to truncated or invalid JSON.", lastException?.ToString());

            return result.UserStories;
        }

        private async Task<List<GeneratedStoryTasks>> GenerateTasksBatchedAsync(
            Kernel kernel,
            List<GeneratedUserStory> stories,
            List<string> techStack,
            List<string> availableSkills,
            CancellationToken cancellationToken)
        {
            var prompt = await _promptLoader.LoadAsync("Planning/WbsTaskGeneration.yaml");
            
            prompt += "\n" +
                      "  CRITICAL: Your response must be a single complete valid JSON object with no trailing text.\n" +
                      "  Do not truncate or cut off the response under any circumstances.\n";

            var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                MaxTokens = 32768,
                Temperature = 0.0,
                Seed = 42,
                ResponseFormat = "json_object"
            };

            int batchSize = 25; // As per the revised gpt-4.1 plan
            var batches = stories.Select((story, index) => new { story, index })
                                 .GroupBy(x => x.index / batchSize)
                                 .Select(g => g.Select(x => x.story).ToList())
                                 .ToList();

            var tasks = new List<Task<List<GeneratedStoryTasks>>>();

            foreach (var batch in batches)
            {
                tasks.Add(GenerateTaskBatchAsync(kernel, prompt, executionSettings, batch, techStack, availableSkills, cancellationToken));
            }

            var results = await Task.WhenAll(tasks);
            return results.SelectMany(r => r).ToList();
        }

        private async Task<List<GeneratedStoryTasks>> GenerateTaskBatchAsync(
            Kernel kernel,
            string prompt,
            PromptExecutionSettings executionSettings,
            List<GeneratedUserStory> batch,
            List<string> techStack,
            List<string> availableSkills,
            CancellationToken cancellationToken)
        {
            // Create simplified DTO for task generation input (avoids sending tasks list again)
            var batchInput = batch.Select(s => new {
                Id = s.Id,
                TitleEn = s.TitleEn,
                DescriptionEn = s.DescriptionEn,
                AcceptanceCriteriaEn = s.AcceptanceCriteriaEn
            });

            var arguments = new KernelArguments(executionSettings)
            {
                ["userStoriesBatch"] = JsonSerializer.Serialize(batchInput),
                ["techStack"] = techStack != null && techStack.Any() ? string.Join(", ", techStack) : "Not specified — use best practices",
                ["availableSkills"] = availableSkills != null && availableSkills.Any() ? string.Join(", ", availableSkills) : "None"
            };

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var function = KernelFunctionYaml.FromPromptYaml(prompt);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var invokeResult = await kernel.InvokeAsync(function, arguments, cancellationToken);
                    sw.Stop();

                    _telemetry.RecordCall(invokeResult.Metadata, sw.ElapsedMilliseconds, "WBSGenerationAgent_Tasks", ModelConstants.MorePowerfulModel, _logger);

                    var rawJson = invokeResult.ToString();
                    var jsonToParse = TryRepairJson(rawJson, isTasks: true);
                    var result = JsonSerializer.Deserialize<GeneratedStoryTasksBatch>(jsonToParse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (result != null && result.StoryTasks != null)
                    {
                        return result.StoryTasks;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("WBS Task JSON parse failed on attempt {Attempt}/3.", attempt);
                }
            }

            _logger.LogWarning("WBS Task generation failed for a batch after 3 attempts. Returning empty task list for this batch.");
            return new List<GeneratedStoryTasks>();
        }

        private string TryRepairJson(string raw, bool isTasks = false)
        {
            raw = raw.Trim();
            if (raw.StartsWith("```json"))
            {
                raw = raw.Substring(7);
                if (raw.EndsWith("```"))
                {
                    raw = raw.Substring(0, raw.Length - 3);
                }
                raw = raw.Trim();
            }
            else if (raw.StartsWith("```"))
            {
                raw = raw.Substring(3);
                if (raw.EndsWith("```"))
                {
                    raw = raw.Substring(0, raw.Length - 3);
                }
                raw = raw.Trim();
            }

            if (!raw.StartsWith("{")) return raw;
            if (raw.EndsWith("}")) return raw;

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
                    if (depth == 1) 
                        lastClosingBrace = i;
                    else if (depth == 0)
                        return raw; 
                }
            }

            if (lastClosingBrace == -1) return raw;

            return raw.Substring(0, lastClosingBrace + 1) + "\n  ]\n}";
        }
    }
}
