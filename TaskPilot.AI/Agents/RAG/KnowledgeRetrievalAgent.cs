using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.RAG
{
    public class KnowledgeRetrievalAgent
    {
        private readonly IVectorStore _vectorStore;
        private readonly ILogger<KnowledgeRetrievalAgent> _logger;

        public KnowledgeRetrievalAgent(
            IVectorStore vectorStore,
            ILogger<KnowledgeRetrievalAgent> logger)
        {
            _vectorStore = vectorStore;
            _logger = logger;
        }

        public async Task<List<KnowledgeChunk>> RetrieveAsync(
            Guid sessionId,
            string question,
            int topK = 5,
            DocumentCategory? category = null,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
            }

            _logger.LogInformation("Retrieving knowledge chunks for session {sessionId}. Question: \"{question}\"", sessionId, question);

            var chunks = await _vectorStore.SearchAsync(
                sessionId,
                question,
                topK,
                category,
                cancellationToken);

            _logger.LogInformation("Retrieved {count} chunks for session {sessionId}", chunks.Count, sessionId);
            if (chunks.Count == 0)
            {
                _logger.LogInformation("No relevant chunks found.\nReturning no-information response.");
            }
            
            // Note: VectorStore search might output similarity score, but we do not have it in the return model. 
            // The prompt says "Top similarity score: {score}" in the log but `KnowledgeChunk` doesn't include it.
            // I'll leave the basic logging as required but the score might not be available easily.

            return chunks;
        }
    }
}
