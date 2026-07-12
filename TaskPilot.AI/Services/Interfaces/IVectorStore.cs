using System;
using TaskPilot.Models.Enums;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Ingestion;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IVectorStore
    {
        Task EnsureCollectionsAsync(CancellationToken cancellationToken = default);

        Task UpsertAsync(
            KnowledgeCollectionType collectionType,
            List<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default);

        Task<List<KnowledgeChunk>> SearchAsync(
            KnowledgeCollectionType collectionType,
            Guid? requirementSessionId,
            Guid? projectId,
            Guid? companyId,
            string queryText,
            int topK = 5,
            float scoreThreshold = 0.75f,
            DocumentCategory? categoryFilter = null,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            KnowledgeCollectionType collectionType,
            Guid documentId,
            Guid? requirementSessionId,
            Guid? projectId,
            Guid? companyId,
            CancellationToken cancellationToken = default);

        Task PromoteKnowledgeAsync(
            KnowledgeCollectionType collectionType,
            Guid projectId,
            IEnumerable<Guid> chunkIds,
            CancellationToken cancellationToken = default);
    }
}
