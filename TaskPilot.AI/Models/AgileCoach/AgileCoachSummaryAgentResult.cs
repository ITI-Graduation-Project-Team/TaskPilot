using System.Collections.Generic;
using System.Text.Json.Serialization;
using TaskPilot.DTOs.AI.AgileCoach;

namespace TaskPilot.AI.Models.AgileCoach
{
    public class AgileCoachSummaryAgentResult
    {
        [JsonPropertyName("summaryEn")]
        public AgileCoachSummaryContent SummaryEn { get; set; } = null!;

        [JsonPropertyName("summaryAr")]
        public AgileCoachSummaryContent SummaryAr { get; set; } = null!;
    }

    public class AgileCoachSummaryContent
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = null!;

        [JsonPropertyName("citations")]
        public List<CitationDto> Citations { get; set; } = new();
    }
}
