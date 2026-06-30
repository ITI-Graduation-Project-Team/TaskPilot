using System;

namespace TaskPilot.DTOs.Sprints
{
    public class ConfirmSprintResult
    {
        public Guid SprintId { get; set; }
        public Guid ProjectId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int UserStoriesAssigned { get; set; }
        public int TasksAssigned { get; set; }
    }
}
