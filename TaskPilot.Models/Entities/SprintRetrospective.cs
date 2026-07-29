using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class SprintRetrospective : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint Sprint { get; set; } = null!;

        public double CompletionRate { get; set; }
        public double VelocityRatio { get; set; }
        public decimal TotalEstimatedHours { get; set; }
        public decimal TotalActualHours { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int UnfinishedTasks { get; set; }
        public DateTime GeneratedAt { get; set; }

        public string AnalysisJson { get; set; } = string.Empty;
        public string ImprovementsJson { get; set; } = string.Empty;

        public string WhatWentWellEn { get; set; } = string.Empty;
        public string WhatWentWellAr { get; set; } = string.Empty;
        public string ChallengesEn { get; set; } = string.Empty;
        public string ChallengesAr { get; set; } = string.Empty;
        public string ActionItemsEn { get; set; } = string.Empty;
        public string ActionItemsAr { get; set; } = string.Empty;
        public decimal EstimationAccuracy { get; set; }
        public decimal ExpectedHours { get; set; }
        public decimal ActualHours { get; set; }
        public string TeamSentimentSummaryEn { get; set; } = string.Empty;
        public string TeamSentimentSummaryAr { get; set; } = string.Empty;
    }
}
