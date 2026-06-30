namespace TaskPilot.DTOs.Planning
{
    public sealed class SuggestedStoryDto
    {
        public Guid StoryId { get; set; }

        public string TitleEn { get; set; } = string.Empty;

        public string TitleAr { get; set; } = string.Empty;

        public decimal EstimatedHours { get; set; }

        public int PriorityScore { get; set; }

        public string ReasonEn { get; set; } = string.Empty;

        public string ReasonAr { get; set; } = string.Empty;
    }
}
