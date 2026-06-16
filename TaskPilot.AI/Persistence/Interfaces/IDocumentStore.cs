using TaskPilot.AI.Models.Ingestion;

namespace TaskPilot.AI.Persistence.Interfaces
{
    public interface IDocumentStore
    {
        Task SaveDocumentAsync(IngestedDocument document, CancellationToken cancellationToken = default);

        Task<IngestedDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

        Task SaveChunksAsync(List<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);

        Task<List<KnowledgeChunk>> GetChunksAsync(Guid documentId, CancellationToken cancellationToken = default);
    }
}
