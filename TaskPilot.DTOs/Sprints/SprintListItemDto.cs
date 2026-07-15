using System;

namespace TaskPilot.DTOs.Sprints
{
    public class SprintListItemDto
    {
        public Guid SprintId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? SprintGoalEn { get; set; }
        public string? SprintGoalAr { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int UserStoriesCount { get; set; }
        public int TasksCount { get; set; }
    }
}
