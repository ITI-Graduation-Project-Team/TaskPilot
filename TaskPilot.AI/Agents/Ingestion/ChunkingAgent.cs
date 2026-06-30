#pragma warning disable SKEXP0050
using Microsoft.SemanticKernel.Text;
using TaskPilot.AI.Models.Ingestion;

namespace TaskPilot.AI.Agents.Ingestion
{
    public class ChunkingAgent
    {
        public Task<List<KnowledgeChunk>> ChunkContentAsync(
            Guid documentId,
            string text,
            int chunkSize = 1000,
            int overlap = 200,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<KnowledgeChunk>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(chunks);
            }

            var lines = TextChunker.SplitPlainTextLines(text, 100);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, chunkSize, overlap);

            int index = 0;
            foreach (var chunkText in paragraphs)
            {
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(new KnowledgeChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        Content = chunkText.Trim(),
                        ChunkIndex = index++,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            return Task.FromResult(chunks);
        }
    }
}
#pragma warning restore SKEXP0050
