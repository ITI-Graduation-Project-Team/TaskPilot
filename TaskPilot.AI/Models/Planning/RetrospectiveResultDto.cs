namespace TaskPilot.AI.Models.Planning
{
    public class RetrospectiveResultDto
    {
        public string WhatWentWellEn { get; set; } = string.Empty;
        public string WhatWentWellAr { get; set; } = string.Empty;
        public string ChallengesEn { get; set; } = string.Empty;
        public string ChallengesAr { get; set; } = string.Empty;
        public string ActionItemsEn { get; set; } = string.Empty;
        public string ActionItemsAr { get; set; } = string.Empty;
        public string TeamSentimentSummary { get; set; } = string.Empty;
    }
}
