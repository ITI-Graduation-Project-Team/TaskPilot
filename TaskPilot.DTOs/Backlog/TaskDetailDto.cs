using System;

namespace TaskPilot.DTOs.Backlog
{
    public class TaskDetailDto
    {
        public Guid Id { get; set; }
        public Guid UserStoryId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? TechnicalSummaryEn { get; set; }
        public string? TechnicalSummaryAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public decimal EstimatedHours { get; set; }
        public string EffortSize { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
