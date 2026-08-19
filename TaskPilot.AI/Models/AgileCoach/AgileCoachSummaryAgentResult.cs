using System.Text.Json.Serialization;

namespace TaskPilot.AI.Models.AgileCoach
{
    public class AgileCoachSummaryAgentResult
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
