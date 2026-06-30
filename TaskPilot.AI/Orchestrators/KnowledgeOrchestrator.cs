using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.RAG;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.RAG;
using TaskPilot.AI.Persistence.Interfaces;

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

        public async Task<KnowledgeAnswerResult> AskAsync(
            Guid sessionId,
            string question,
            int topK = 5,
            DocumentCategory? category = null,
            CancellationToken cancellationToken = default)
        {
            var chunks = await _retrievalAgent.RetrieveAsync(
                sessionId, 
                question, 
                topK, 
                category, 
                cancellationToken);

            if (chunks.Count == 0)
            {
                return new KnowledgeAnswerResult
                {
                    Answer = "The uploaded documents do not contain enough information.",
                    Sources = new List<KnowledgeSource>()
                };
            }

            var answer = await _answerAgent.GenerateAsync(question, chunks, cancellationToken);

            var sources = new List<KnowledgeSource>();
            
            // To prevent N+1 document fetch problem, group by DocumentId and fetch unique documents
            var distinctDocumentIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
            var documentMap = new Dictionary<Guid, string>();
            
            foreach (var docId in distinctDocumentIds)
            {
                var doc = await _documentStore.GetDocumentAsync(docId, cancellationToken);
                if (doc != null)
                {
                    documentMap[docId] = doc.FileName;
                }
            }

            foreach (var chunk in chunks)
            {
                sources.Add(new KnowledgeSource
                {
                    DocumentId = chunk.DocumentId,
                    ChunkId = chunk.Id,
                    FileName = documentMap.TryGetValue(chunk.DocumentId, out var fileName) ? fileName : string.Empty,
                    Category = chunk.Category
                });
            }

            _logger.LogInformation("Sources used: {sourceCount}", sources.Count);

            return new KnowledgeAnswerResult
            {
                Answer = answer,
                Sources = sources
            };
        }
    }
}
