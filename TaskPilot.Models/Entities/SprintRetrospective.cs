using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class SprintRetrospective : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint Sprint { get; set; } = null!;

        public string WhatWentWellEn { get; set; } = string.Empty;
        public string WhatWentWellAr { get; set; } = string.Empty;

        public string ChallengesEn { get; set; } = string.Empty;
        public string ChallengesAr { get; set; } = string.Empty;

        public string ActionItemsEn { get; set; } = string.Empty;
        public string ActionItemsAr { get; set; } = string.Empty;

        public double CompletionRate { get; set; }
        public decimal EstimationAccuracy { get; set; }
        public decimal ExpectedHours { get; set; }
        public decimal ActualHours { get; set; }
        public string TeamSentimentSummaryEn { get; set; } = string.Empty;
        public string TeamSentimentSummaryAr { get; set; } = string.Empty;
    }
}
