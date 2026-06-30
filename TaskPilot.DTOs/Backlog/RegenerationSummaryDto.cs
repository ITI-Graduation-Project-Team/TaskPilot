namespace TaskPilot.DTOs.Backlog
{
    public class RegenerationSummaryDto
    {
        public Guid ProjectId { get; set; }
        public int DeletedUserStories { get; set; }
        public int DeletedTasks { get; set; }
        public int GeneratedUserStories { get; set; }
        public int GeneratedTasks { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
