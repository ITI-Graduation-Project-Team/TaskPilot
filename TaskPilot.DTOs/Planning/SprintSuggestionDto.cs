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
        
        public string CapacityExplanationEn { get; set; } = string.Empty;
        
        public string CapacityExplanationAr { get; set; } = string.Empty;

        public List<string> RisksEn { get; set; } = new();

        public List<string> RisksAr { get; set; } = new();

        public List<SuggestedStoryDto> Stories { get; set; } = new();

        public List<ExcludedStoryDto> ExcludedStories { get; set; } = new();
    }
}
