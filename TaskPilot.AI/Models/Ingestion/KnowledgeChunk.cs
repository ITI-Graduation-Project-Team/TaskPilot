using System;

namespace TaskPilot.AI.Models.Ingestion
{
    public class KnowledgeChunk
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DocumentId { get; set; }

        public Guid? RequirementSessionId { get; set; }

        public Guid? ProjectId { get; set; }

        public Guid? CompanyId { get; set; }

        public TaskPilot.AI.Enums.DocumentCategory Category { get; set; }

        public string SourceFile { get; set; } = string.Empty;

        public string DocumentType { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
