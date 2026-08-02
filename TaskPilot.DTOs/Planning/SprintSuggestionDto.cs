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

        public List<string> Risks { get; set; } = new();

        public List<SuggestedStoryDto> Stories { get; set; } = new();
    }
}
