namespace TaskPilot.DTOs.Planning
{
    public sealed class SprintSuggestionDto
    {
        public string SprintGoalEn { get; set; } = string.Empty;

        public string SprintGoalAr { get; set; } = string.Empty;

        public decimal TotalEstimatedHours { get; set; }

        public List<string> Risks { get; set; } = new();

        public List<SuggestedStoryDto> Stories { get; set; } = new();
    }
}
