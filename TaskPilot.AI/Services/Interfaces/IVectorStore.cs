using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Ingestion;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IVectorStore
    {
        Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

        Task UpsertAsync(
            List<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default);

        Task<List<KnowledgeChunk>> SearchAsync(
            Guid sessionId,
            string queryText,
            int topK = 5,
            DocumentCategory? categoryFilter = null,
            CancellationToken cancellationToken = default);
    }
}
