using TaskPilot.AI.Models.ContextAdvisor;

namespace TaskPilot.AI.RAG
{
    public interface IProjectKnowledgeSearchService
    {
        Task<List<RetrievedKnowledgeChunk>> SearchAsync(
            Guid? projectId,
            string query,
            int topK,
            CancellationToken cancellationToken = default);
    }
}
