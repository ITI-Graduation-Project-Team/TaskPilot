using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Agents.RAG;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Models.AgileCoach;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.AI.AgileCoach;

namespace TaskPilot.AI.Agents
{
    public class AgileCoachAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly KnowledgeRetrievalAgent _retrievalAgent;
        private readonly IDocumentStore _documentStore;

        public AgileCoachAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            KnowledgeRetrievalAgent retrievalAgent,
            IDocumentStore documentStore)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _retrievalAgent = retrievalAgent;
            _documentStore = documentStore;
        }

        private string BuildContextChunks(List<KnowledgeChunk> chunks, Dictionary<Guid, string> documentMap)
        {
            var chunkStrings = chunks.Select((c, index) => 
            {
                var displayName = documentMap.TryGetValue(c.DocumentId, out var name) ? name : "Unknown Document";
                return $"[{index + 1}] {displayName}: {c.Content}";
            });
            return string.Join("\n", chunkStrings);
        }

        private List<CitationDto> MapToCitations(List<KnowledgeChunk> chunks, Dictionary<Guid, string> documentMap)
        {
            return chunks.Select(chunk => new CitationDto
            {
                SourceDocument = chunk.DocumentId.ToString(),
                SourceDocumentDisplayName = documentMap.TryGetValue(chunk.DocumentId, out var name) ? name : "Unknown Document",
                ChunkExcerpt = chunk.Content
            }).ToList();
        }

        public async Task<AgileCoachSummaryAgentResult> GenerateSummaryAsync(
            string taskTitle,
            string taskDescription,
            Guid requirementsSessionId,
            string lang)
        {
            var chunks = await _retrievalAgent.RetrieveAsync(
                requirementsSessionId,
                $"{taskTitle} {taskDescription}",
                topK: 5);

            if (chunks == null || chunks.Count == 0)
            {
                throw new AgileCoachException("No relevant context found in the knowledge base for this task.");
            }

            var distinctDocumentIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
            var documentMap = new Dictionary<Guid, string>();
            foreach (var id in distinctDocumentIds)
            {
                var doc = await _documentStore.GetDocumentAsync(id);
                if (doc != null)
                {
                    documentMap[id] = doc.FileName;
                }
            }

            var contextChunksString = BuildContextChunks(chunks, documentMap);

            var prompt = await _promptLoader.LoadAsync("AgileCoach/agile_coach_summary.yaml");

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);

            var invokeResult = await kernel.InvokeAsync(
                KernelFunctionYaml.FromPromptYaml(prompt),
                new KernelArguments
                {
                    ["task_title"] = taskTitle,
                    ["task_description"] = taskDescription,
                    ["context_chunks"] = contextChunksString,
                    ["lang"] = lang
                });

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

            result.Citations = MapToCitations(chunks, documentMap);

            return result;
        }

        public async IAsyncEnumerable<string> StreamChatAsync(
            string userMessage,
            List<ChatMessageDto> history,
            Guid requirementsSessionId,
            string lang)
        {
            var chunks = await _retrievalAgent.RetrieveAsync(
                requirementsSessionId,
                userMessage,
                topK: 5);

            if (chunks == null || chunks.Count == 0)
            {
                yield return "[No relevant context found in the project knowledge base for this question.]";
                yield break;
            }

            var distinctDocumentIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
            var documentMap = new Dictionary<Guid, string>();
            foreach (var id in distinctDocumentIds)
            {
                var doc = await _documentStore.GetDocumentAsync(id);
                if (doc != null)
                {
                    documentMap[id] = doc.FileName;
                }
            }

            var contextChunksString = BuildContextChunks(chunks, documentMap);

            var conversationHistory = string.Join("\n", history.Select(m => 
                $"{(m.Role == "user" ? "User" : "Assistant")}: {m.Content}"));

            var prompt = await _promptLoader.LoadAsync("AgileCoach/agile_coach_chat.yaml");

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);

            var stream = kernel.InvokeStreamingAsync<string>(
                KernelFunctionYaml.FromPromptYaml(prompt),
                new KernelArguments
                {
                    ["user_message"] = userMessage,
                    ["context_chunks"] = contextChunksString,
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
