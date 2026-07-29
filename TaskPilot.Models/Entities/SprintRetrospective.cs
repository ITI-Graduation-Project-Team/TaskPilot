using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class SprintRetrospective : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint Sprint { get; set; } = null!;

        // Raw Metrics (always accurate — from C#)
        public double CompletionRate { get; set; }
        public double VelocityRatio { get; set; }
        public decimal TotalEstimatedHours { get; set; }
        public decimal TotalActualHours { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int UnfinishedTasks { get; set; }

        // AI Analysis (stored as JSON)
        public string AnalysisJson { get; set; } = string.Empty;

        // Improvements (stored as JSON — consumed by SprintSuggestionAgent)
        public string ImprovementsJson { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
