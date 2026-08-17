namespace TaskPilot.DTOs.Planning
{
    public sealed class ExcludedStoryDto
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
