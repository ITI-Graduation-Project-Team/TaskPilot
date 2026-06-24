using TaskPilot.AI.Models.Ingestion;

namespace TaskPilot.AI.Agents.Ingestion
{
    public class ChunkingAgent
    {
        public Task<List<KnowledgeChunk>> ChunkContentAsync(
            Guid documentId,
            string text,
            Guid? projectId = null,
            int chunkSize = 1000,
            int overlap = 200,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<KnowledgeChunk>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(chunks);
            }

            int index = 0;
            int startIndex = 0;
            while (startIndex < text.Length)
            {
                int length = Math.Min(chunkSize, text.Length - startIndex);
                var chunkText = text.Substring(startIndex, length).Trim();
                if (!string.IsNullOrEmpty(chunkText))
                {
                    chunks.Add(new KnowledgeChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        ProjectId = projectId,
                        Content = chunkText,
                        ChunkIndex = index++,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (startIndex + length >= text.Length)
                {
                    break;
                }

                startIndex += (chunkSize - overlap);
            }

            return Task.FromResult(chunks);
        }
    }
}
