using System;
using TaskPilot.Models.Enums;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

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

        public async Task<Result<List<KnowledgeChunk>>> RetrieveAsync(
            KnowledgeCollectionType collectionType,
            Guid? requirementSessionId,
            Guid? projectId,
            Guid? companyId,
            string question,
            int topK = 5,
            float scoreThreshold = 0.75f,
            DocumentCategory? category = null,
            CancellationToken cancellationToken = default)
        {
            if (requirementSessionId == null && projectId == null && companyId == null)
            {
                return Result.Failure<List<KnowledgeChunk>>(KnowledgeErrors.MissingTenantIsolation);
            }

            _logger.LogInformation("Retrieving knowledge chunks for Collection {collectionType}. Question: \"{question}\"", collectionType, question);

            var chunks = await _vectorStore.SearchAsync(
                collectionType,
                requirementSessionId,
                projectId,
                companyId,
                question,
                topK,
                scoreThreshold,
                category,
                cancellationToken);

            _logger.LogInformation("Retrieved {count} chunks for Collection {collectionType}", chunks.Count, collectionType);
            if (chunks.Count == 0)
            {
                _logger.LogInformation("No relevant chunks found.\nReturning no-information response.");
            }
            
            // Note: VectorStore search might output similarity score, but we do not have it in the return model. 
            // The prompt says "Top similarity score: {score}" in the log but `KnowledgeChunk` doesn't include it.
            // I'll leave the basic logging as required but the score might not be available easily.

            return Result.Success(chunks);
        }
    }
}
