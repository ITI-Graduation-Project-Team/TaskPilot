using System;

namespace TaskPilot.DTOs.Sprints
{
    public class ActiveSprintDto
    {
        public Guid SprintId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DaysRemaining { get; set; }
        public double CompletionPercentage { get; set; }
    }
}
