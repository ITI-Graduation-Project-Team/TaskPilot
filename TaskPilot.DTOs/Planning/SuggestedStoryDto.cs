namespace TaskPilot.DTOs.Planning
{
    public sealed class SuggestedStoryDto
    {
        public Guid StoryId { get; set; }

        public string Title { get; set; } = string.Empty;

        public decimal EstimatedHours { get; set; }

        public int PriorityScore { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
