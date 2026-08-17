namespace TaskPilot.DTOs.Planning
{
    public sealed class SprintSuggestionDto
    {
        public int SprintNumber { get; set; } = 1;

        public string SprintTitleEn { get; set; } = string.Empty;

        public string SprintTitleAr { get; set; } = string.Empty;

        public string SprintGoalEn { get; set; } = string.Empty;

        public string SprintGoalAr { get; set; } = string.Empty;

        public decimal TotalEstimatedHours { get; set; }

        /// <summary>
        /// Capacity hours not consumed by the selected stories (TargetHours - UtilizedHours).
        /// Set by the deterministic C# layer after story selection — not the AI.
        /// </summary>
        public decimal UnallocatedCapacityHours { get; set; }

        /// <summary>
        /// True when UtilizedHours fell below MinUtilizationThreshold (85%) of TargetHours,
        /// indicating the backlog may have more eligible stories that could have been included.
        /// </summary>
        public bool IsUnderutilized { get; set; }

        public string CapacityExplanationEn { get; set; } = string.Empty;

        public string CapacityExplanationAr { get; set; } = string.Empty;

        public List<string> RisksEn { get; set; } = new();

        public List<string> RisksAr { get; set; } = new();

        public List<SuggestedStoryDto> Stories { get; set; } = new();

        public List<ExcludedStoryDto> ExcludedStories { get; set; } = new();
    }
}
