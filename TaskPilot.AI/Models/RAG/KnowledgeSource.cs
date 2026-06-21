using System;
using TaskPilot.AI.Enums;

namespace TaskPilot.AI.Models.RAG
{
    public class KnowledgeSource
    {
        public Guid DocumentId { get; set; }

        public Guid ChunkId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public DocumentCategory Category { get; set; }
    }
}
