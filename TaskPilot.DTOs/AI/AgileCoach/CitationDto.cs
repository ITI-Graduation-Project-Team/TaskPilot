using System.Text.Json.Serialization;

namespace TaskPilot.DTOs.AI.AgileCoach
{
    public class CitationDto
    {
        [JsonPropertyName("sourceDocument")]
        public string SourceDocument { get; set; } = string.Empty;

        [JsonPropertyName("sourceDocumentDisplayName")]
        public string SourceDocumentDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("chunkExcerpt")]
        public string ChunkExcerpt { get; set; } = string.Empty;
    }
}
