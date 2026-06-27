namespace TaskPilot.DTOs.Sprint
{
    public class SprintRetrospectiveResponseDto
    {
        public Guid Id { get; set; }
        public Guid SprintId { get; set; }
        public string WhatWentWellEn { get; set; } = string.Empty;
        public string WhatWentWellAr { get; set; } = string.Empty;
        public string ChallengesEn { get; set; } = string.Empty;
        public string ChallengesAr { get; set; } = string.Empty;
        public string ActionItemsEn { get; set; } = string.Empty;
        public string ActionItemsAr { get; set; } = string.Empty;
        public double CompletionRate { get; set; }
        public decimal EstimationAccuracy { get; set; }
        public string TeamSentimentSummaryEn { get; set; } = string.Empty;
        public string TeamSentimentSummaryAr { get; set; } = string.Empty;
    }
}
