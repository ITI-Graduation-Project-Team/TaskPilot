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
            int chunkSize = 1200,
            int overlap = 300,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<KnowledgeChunk>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(chunks);
            }

            var lines = TextChunker.SplitPlainTextLines(text, 100);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, chunkSize, overlap);

            using var md5 = System.Security.Cryptography.MD5.Create();

            int index = 0;
            foreach (var chunkText in paragraphs)
            {
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    var hashData = System.Text.Encoding.UTF8.GetBytes($"{documentId}_{index}");
                    var chunkId = new Guid(md5.ComputeHash(hashData));

                    chunks.Add(new KnowledgeChunk
                    {
                        Id = chunkId,
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
