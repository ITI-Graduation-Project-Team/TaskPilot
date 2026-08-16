using System;
using TaskPilot.Models.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.RAG;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.RAG;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.AI.Orchestrators
{
    public class KnowledgeOrchestrator
    {
        private readonly KnowledgeRetrievalAgent _retrievalAgent;
        private readonly KnowledgeAnswerAgent _answerAgent;
        private readonly IDocumentStore _documentStore;
        private readonly ILogger<KnowledgeOrchestrator> _logger;

        public KnowledgeOrchestrator(
            KnowledgeRetrievalAgent retrievalAgent,
            KnowledgeAnswerAgent answerAgent,
            IDocumentStore documentStore,
            ILogger<KnowledgeOrchestrator> logger)
        {
            _retrievalAgent = retrievalAgent;
            _answerAgent = answerAgent;
            _documentStore = documentStore;
            _logger = logger;
        }

        public async Task<Result<KnowledgeAnswerResult>> AskAsync(
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
            var chunksResult = await _retrievalAgent.RetrieveAsync(
                collectionType, 
                requirementSessionId,
                projectId, 
                companyId,
                question, 
                topK, 
                scoreThreshold,
                category, 
                cancellationToken);

            if (chunksResult.IsFailure)
            {
                return Result.Failure<KnowledgeAnswerResult>(chunksResult.Error);
            }

            var chunks = chunksResult.Value;

            if (chunks.Count == 0)
            {
                return Result.Success(new KnowledgeAnswerResult
                {
                    Answer = "The uploaded documents do not contain enough information.",
                    Sources = new List<KnowledgeSource>()
                });
            }

            var answer = await _answerAgent.GenerateAsync(question, chunks, projectId.GetValueOrDefault(), cancellationToken);

            var sources = new List<KnowledgeSource>();

            foreach (var chunk in chunks)
            {
                sources.Add(new KnowledgeSource
                {
                    DocumentId = chunk.DocumentId,
                    ChunkId = chunk.Id,
                    FileName = chunk.SourceFile,
                    Category = chunk.Category
                });
            }

            _logger.LogInformation("Sources used: {sourceCount}", sources.Count);

            return Result.Success(new KnowledgeAnswerResult
            {
                Answer = answer,
                Sources = sources
            });
        }
    }
}
