using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.RAG;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Models.AgileCoach;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.AI.AgileCoach;

namespace TaskPilot.AI.Agents
{
    public class AgileCoachAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly KnowledgeRetrievalAgent _retrievalAgent;
        private readonly ILogger<AgileCoachAgent> _logger;

        public AgileCoachAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            KnowledgeRetrievalAgent retrievalAgent,
            ILogger<AgileCoachAgent> logger)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _retrievalAgent = retrievalAgent;
            _logger = logger;
        }

        private string BuildContextChunks(List<KnowledgeChunk> chunks)
        {
            var chunkStrings = chunks.Select((c, index) => 
            {
                var categoryInfo = !string.IsNullOrEmpty(c.Category.ToString()) ? $" | Category: {c.Category}" : "";
                var content = c.Content.Length > 1600 ? c.Content[..1600] : c.Content;
                return $"[BRD Chunk {index + 1}]{categoryInfo}\n{content}";
            });
            return string.Join("\n\n---\n\n", chunkStrings);
        }

        public async Task<AgileCoachSummaryAgentResult> GenerateSummaryAsync(
            string taskTitle,
            string taskDescription,
            Guid projectId,
            string lang,
            string snapshotContext,
            string qaContext,
            string userStoryContext,
            CancellationToken cancellationToken = default)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var retrievalStopwatch = Stopwatch.StartNew();
            var chunksResult = await _retrievalAgent.RetrieveAsync(
                TaskPilot.Models.Enums.KnowledgeCollectionType.ProjectPolicies,
                null,
                projectId,
                null,
                $"{taskTitle} {taskDescription}",
                topK: 5,
                scoreThreshold: 0.65f,
                cancellationToken: cancellationToken);
            retrievalStopwatch.Stop();

            var chunks = chunksResult.IsSuccess && chunksResult.Value != null ? chunksResult.Value : new List<KnowledgeChunk>();
            var contextChunksString = BuildContextChunks(chunks);

            var prompt = await _promptLoader.LoadAsync("AgileCoach/agile_coach_summary.yaml");

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 700,
                Temperature = 0.1,
                ResponseFormat = "json_object"
            };

            var generationStopwatch = Stopwatch.StartNew();
            var invokeResult = await kernel.InvokeAsync(
                KernelFunctionYaml.FromPromptYaml(prompt),
                new KernelArguments(executionSettings)
                {
                    ["task_title"] = taskTitle,
                    ["task_description"] = taskDescription,
                    ["context_chunks"] = contextChunksString,
                    ["snapshot_context"] = snapshotContext,
                    ["qa_context"] = qaContext,
                    ["user_story_context"] = userStoryContext,
                    ["lang"] = lang,
                    ["projectId"] = projectId.ToString()
                },
                cancellationToken: cancellationToken);
            generationStopwatch.Stop();

            var rawJson = invokeResult.ToString();
            
            AgileCoachSummaryAgentResult? result;
            try
            {
                result = JsonSerializer.Deserialize<AgileCoachSummaryAgentResult>(rawJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                throw new AgileCoachException("Failed to parse the AI model output.", ex);
            }

            if (result == null)
            {
                throw new AgileCoachException("Parsed result was null.");
            }

            if (string.IsNullOrWhiteSpace(result.Content))
            {
                throw new AgileCoachException("The AI model returned an empty summary.");
            }

            totalStopwatch.Stop();
            _logger.LogInformation(
                "Agile Coach summary generated for ProjectId {ProjectId}. Language: {Language}, Chunks: {ChunkCount}, RetrievalMs: {RetrievalMs}, GenerationMs: {GenerationMs}, TotalMs: {TotalMs}",
                projectId,
                lang,
                chunks.Count,
                retrievalStopwatch.ElapsedMilliseconds,
                generationStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds);

            return result;
        }

        public async IAsyncEnumerable<string> StreamChatAsync(
            string userMessage,
            List<ChatMessageDto> history,
            Guid projectId,
            string lang,
            string taskTitle,
            string taskDescription,
            string snapshotContext,
            string qaContext,
            string userStoryContext)
        {
            var queryText = $"{taskTitle} {taskDescription} {userMessage}";

            var chunksResult = await _retrievalAgent.RetrieveAsync(
                TaskPilot.Models.Enums.KnowledgeCollectionType.ProjectPolicies,
                null,
                projectId,
                null,
                queryText,
                topK: 10);

            var chunks = chunksResult.IsSuccess && chunksResult.Value != null ? chunksResult.Value : new List<KnowledgeChunk>();
            var contextChunksString = BuildContextChunks(chunks);

            var conversationHistory = string.Join("\n", history.Select(m => 
                $"{(m.Role == "user" ? "User" : "Assistant")}: {m.Content}"));

            var prompt = await _promptLoader.LoadAsync("AgileCoach/agile_coach_chat.yaml");

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);

            var stream = kernel.InvokeStreamingAsync<string>(
                KernelFunctionYaml.FromPromptYaml(prompt),
                new KernelArguments
                {
                    ["task_title"] = taskTitle,
                    ["user_message"] = userMessage,
                    ["context_chunks"] = contextChunksString,
                    ["snapshot_context"] = snapshotContext,
                    ["qa_context"] = qaContext,
                    ["user_story_context"] = userStoryContext,
                    ["conversation_history"] = conversationHistory,
                    ["lang"] = lang
                });

            await foreach (var chunk in stream)
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    yield return chunk;
                }
            }
        }
    }
}
