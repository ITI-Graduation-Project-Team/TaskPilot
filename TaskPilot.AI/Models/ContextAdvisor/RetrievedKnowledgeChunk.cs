namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class RetrievedKnowledgeChunk
    {
        public Guid ChunkId { get; set; }

        public Guid DocumentId { get; set; }

        public Guid? ProjectId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        public string Content { get; set; } = string.Empty;

        public double Score { get; set; }

        public string? SourceUrl { get; set; }
    }
}
