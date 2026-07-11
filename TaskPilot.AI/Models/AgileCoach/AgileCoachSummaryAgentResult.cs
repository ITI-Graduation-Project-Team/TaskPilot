using System.Collections.Generic;
using System.Text.Json.Serialization;
using TaskPilot.DTOs.AI.AgileCoach;

namespace TaskPilot.AI.Models.AgileCoach
{
    public class AgileCoachSummaryAgentResult
    {
        [JsonPropertyName("codebaseNotes")]
        public string CodebaseNotes { get; set; } = string.Empty;

        [JsonPropertyName("relatedPastTasks")]
        public string RelatedPastTasks { get; set; } = string.Empty;

        [JsonPropertyName("techStackContext")]
        public string TechStackContext { get; set; } = string.Empty;

        [JsonPropertyName("suggestedImplementationGuidance")]
        public string SuggestedImplementationGuidance { get; set; } = string.Empty;

        [JsonPropertyName("citations")]
        public List<CitationDto> Citations { get; set; } = new();
    }
}
