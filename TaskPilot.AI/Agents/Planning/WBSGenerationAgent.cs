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

            // Phase 1: Generate Stories (English only — Arabic stripped in Fix 1)
            var stories = await GenerateStoriesAsync(kernel, snapshot, techStack, platformTargets, projectType, availableSkills, brdContext, cancellationToken);
            
            // Phase 2: Generate Tasks in Batches (English only, parallel batches)
            var storyTasks = await GenerateTasksBatchedAsync(kernel, stories, techStack, availableSkills, cancellationToken);

            // Merge Tasks into Stories
            foreach (var story in stories)
            {
                var generatedTasks = storyTasks.FirstOrDefault(st => st.StoryId == story.Id)?.Tasks;
                if (generatedTasks != null)
                    story.Tasks = generatedTasks;
            }

            // Phase 3: Translate English content to Arabic (Fix 1 — separate lightweight call)
            await TranslateWbsAsync(kernel, stories, cancellationToken);

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
                    if (jsonToParse == null)
                    {
                        _logger.LogWarning("WBS Story JSON repair returned null on attempt {Attempt}/3 — triggering retry.", attempt);
                        continue;
                    }
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
                MaxTokens = 16384, // Fix 1: reduced from 32768 — English-only output well within 16k
                Temperature = 0.0,
                Seed = 42,
                ResponseFormat = "json_object"
            };

            int batchSize = 8; // Fix 1: reduced from 25 — English-only output fits comfortably per batch
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
                    if (jsonToParse == null)
                    {
                        _logger.LogWarning("WBS Task JSON repair returned null on attempt {Attempt}/3 — triggering retry.", attempt);
                        continue;
                    }
                    var result = JsonSerializer.Deserialize<GeneratedStoryTasksBatch>(jsonToParse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (result != null && result.StoryTasks != null)
                        return result.StoryTasks;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("WBS Task JSON parse failed on attempt {Attempt}/3.", attempt);
                }
            }

            _logger.LogWarning("WBS Task generation failed for a batch after 3 attempts. Returning empty task list for this batch.");
            return new List<GeneratedStoryTasks>();
        }

        /// <summary>
        /// Fix 1 Phase 3: Translates all English story and task fields to Arabic using gpt-4o-mini.
        /// Called only after English generation is confirmed valid. On failure, logs a warning
        /// and leaves Ar fields as empty strings so persistence is not blocked.
        /// </summary>
        private async Task TranslateWbsAsync(
            Kernel kernel,
            List<GeneratedUserStory> stories,
            CancellationToken cancellationToken)
        {
            try
            {
                var translationPrompt = await _promptLoader.LoadAsync("Planning/WbsTranslation.yaml");
                var translationFunction = KernelFunctionYaml.FromPromptYaml(translationPrompt);

                // Use cheap/fast model for translation — create a new kernel for gpt-4o-mini
                var translationKernel = _kernelService.CreateKernel(ModelConstants.CheapModel, "LongRunningAiClient");

                var translationSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
                {
                    MaxTokens = 16384,
                    Temperature = 0.0,
                    Seed = 42,
                    ResponseFormat = "json_object"
                };

                // Build flat list of all items (stories + tasks) — each gets a unique translation id
                var items = new List<object>();
                var storyMap = new Dictionary<string, GeneratedUserStory>();
                var taskMap = new Dictionary<string, GeneratedTask>();

                foreach (var story in stories)
                {
                    var sid = $"S_{story.Id}";
                    items.Add(new { id = sid, type = "story", titleEn = story.TitleEn, descriptionEn = story.DescriptionEn, acceptanceCriteriaEn = story.AcceptanceCriteriaEn });
                    storyMap[sid] = story;

                    foreach (var task in story.Tasks)
                    {
                        var tid = $"T_{story.Id}_{task.TitleEn?.GetHashCode():X}";
                        items.Add(new { id = tid, type = "task", titleEn = task.TitleEn, descriptionEn = task.DescriptionEn, acceptanceCriteriaEn = task.AcceptanceCriteriaEn });
                        taskMap[tid] = task;
                    }
                }

                // Translate in batches of 30 items to stay under 16k max_tokens
                const int translationBatchSize = 30;
                var translationBatches = items.Select((item, idx) => new { item, idx })
                    .GroupBy(x => x.idx / translationBatchSize)
                    .Select(g => g.Select(x => x.item).ToList())
                    .ToList();

                foreach (var batch in translationBatches)
                {
                    var batchJson = System.Text.Json.JsonSerializer.Serialize(batch);
                    var args = new KernelArguments(translationSettings) { ["itemsBatch"] = batchJson };

                    var invokeResult = await translationKernel.InvokeAsync(translationFunction, args, cancellationToken);
                    _telemetry.RecordCall(invokeResult.Metadata, 0, "WBSGenerationAgent_Translation", ModelConstants.CheapModel, _logger);

                    var rawJson = invokeResult.ToString();
                    var translations = System.Text.Json.JsonSerializer.Deserialize<WbsTranslationBatch>(
                        rawJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (translations?.Translations == null) continue;

                    foreach (var t in translations.Translations)
                    {
                        if (storyMap.TryGetValue(t.Id, out var story))
                        {
                            story.TitleAr = t.TitleAr ?? story.TitleAr;
                            story.DescriptionAr = t.DescriptionAr ?? story.DescriptionAr;
                            story.AcceptanceCriteriaAr = t.AcceptanceCriteriaAr ?? story.AcceptanceCriteriaAr;
                        }
                        else if (taskMap.TryGetValue(t.Id, out var task))
                        {
                            task.TitleAr = t.TitleAr ?? task.TitleAr;
                            task.DescriptionAr = t.DescriptionAr ?? task.DescriptionAr;
                            task.AcceptanceCriteriaAr = t.AcceptanceCriteriaAr ?? task.AcceptanceCriteriaAr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Translation failure is non-fatal — English content is already persisted
                _logger.LogWarning(ex, "WBSGenerationAgent: Arabic translation phase failed. WBS will be persisted with English-only content.");
            }
        }

        // DTO for deserializing the translation response
        private sealed class WbsTranslationBatch
        {
            public List<WbsTranslationItem> Translations { get; set; } = new();
        }

        private sealed class WbsTranslationItem
        {
            public string Id { get; set; } = string.Empty;
            public string? TitleAr { get; set; }
            public string? DescriptionAr { get; set; }
            public string? AcceptanceCriteriaAr { get; set; }
        }

        /// <summary>
        /// Fix 6: Improved JSON repair.
        /// Tracks lastSafeClosingBrace — only updated when the scanner is outside a string
        /// (inString==false) at depth==1. Prevents using a "}" inside a truncated string as cut point.
        /// After repair, validates with JsonDocument.Parse; returns null on failure so the retry loop
        /// fires a real LLM re-call rather than deserializing guaranteed-broken JSON.
        /// </summary>
        private string? TryRepairJson(string raw, bool isTasks = false)
        {
            raw = raw.Trim();
            if (raw.StartsWith("```json"))
            {
                raw = raw.Substring(7);
                if (raw.EndsWith("```"))
                    raw = raw.Substring(0, raw.Length - 3);
                raw = raw.Trim();
            }
            else if (raw.StartsWith("```"))
            {
                raw = raw.Substring(3);
                if (raw.EndsWith("```"))
                    raw = raw.Substring(0, raw.Length - 3);
                raw = raw.Trim();
            }

            if (!raw.StartsWith("{")) return raw;

            // Already well-formed — quick validate and return
            if (raw.EndsWith("}"))
                return IsValidJson(raw) ? raw : null;

            // Fix 6: lastSafeClosingBrace — only updated when provably outside a string
            int lastSafeClosingBrace = -1;
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
                        lastSafeClosingBrace = i;  // safe: we are outside any string
                    else if (depth == 0)
                        return IsValidJson(raw) ? raw : null;
                }
            }

            if (lastSafeClosingBrace == -1)
            {
                _logger.LogWarning("TryRepairJson: no safe truncation boundary found. Returning null to trigger retry.");
                return null;
            }

            var repaired = raw.Substring(0, lastSafeClosingBrace + 1) + "\n  ]\n}";

            if (!IsValidJson(repaired))
            {
                _logger.LogWarning("TryRepairJson: repaired JSON still invalid after truncation. Returning null to trigger retry.");
                return null;
            }

            return repaired;
        }

        private static bool IsValidJson(string json)
        {
            try { using var _ = System.Text.Json.JsonDocument.Parse(json); return true; }
            catch (System.Text.Json.JsonException) { return false; }
        }
    }
}
