using System;

namespace TaskPilot.DTOs.Backlog
{
    public class SprintBoardTaskDto
    {
        public Guid TaskId { get; set; }
        public Guid UserStoryId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string? TitleAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
    }
}
