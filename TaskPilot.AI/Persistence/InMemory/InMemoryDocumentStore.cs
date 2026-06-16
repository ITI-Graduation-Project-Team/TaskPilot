using System.Collections.Concurrent;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Persistence.Interfaces;

namespace TaskPilot.AI.Persistence.InMemory
{
    public class InMemoryDocumentStore : IDocumentStore
    {
        private static readonly ConcurrentDictionary<Guid, IngestedDocument> _documents = new();
        private static readonly ConcurrentDictionary<Guid, List<KnowledgeChunk>> _chunks = new();

        public Task SaveDocumentAsync(IngestedDocument document, CancellationToken cancellationToken = default)
        {
            _documents[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task<IngestedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(documentId, out var document);
            return Task.FromResult(document);
        }

        public Task SaveChunksAsync(List<KnowledgeChunk> chunks, CancellationToken cancellationToken = default)
        {
            if (chunks.Any())
            {
                var docId = chunks.First().DocumentId;
                _chunks[docId] = chunks;
            }
            return Task.CompletedTask;
        }

        public Task<List<KnowledgeChunk>> GetChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            _chunks.TryGetValue(documentId, out var chunks);
            return Task.FromResult(chunks ?? new List<KnowledgeChunk>());
        }

        public Task<List<KnowledgeChunk>> GetAvailableChunksAsync(
            Guid? projectId = null,
            CancellationToken cancellationToken = default)
        {
            var availableDocumentIds =
                _documents
                    .Values
                    .Where(document =>
                        document.IsAvailableToContextSummarizer
                        && (projectId is null || document.ProjectId == projectId))
                    .Select(document => document.Id)
                    .ToHashSet();

            var chunks =
                _chunks
                    .Where(entry => availableDocumentIds.Contains(entry.Key))
                    .SelectMany(entry => entry.Value)
                    .ToList();

            return Task.FromResult(chunks);
        }
    }
}
