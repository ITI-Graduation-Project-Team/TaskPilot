namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ContextCitation
    {
        public int Number { get; set; }

        public Guid DocumentId { get; set; }

        public Guid ChunkId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public int ChunkIndex { get; set; }

        public string? SourceUrl { get; set; }

        public string Snippet { get; set; } = string.Empty;
    }
}
