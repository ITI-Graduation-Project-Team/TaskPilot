using System;

namespace TaskPilot.AI.Models.Ingestion
{
    public class VisualAsset
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string CloudinaryUrl { get; set; } = string.Empty;
        public string CloudinaryPublicId { get; set; } = string.Empty;

        // Visual characteristics
        public int PageNumber { get; set; }
        public string BoundingBox { get; set; } = string.Empty;

        // AI-Generated Semantic Metadata
        public string DiagramType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    }
}
