using System;

namespace TaskPilot.AI.Models.Ingestion
{
    public class KnowledgeChunk
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DocumentId { get; set; }

        public string Content { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
