using System;

namespace TaskPilot.DTOs.Backlog
{
    public class TaskItemDto
    {
        public Guid Id { get; set; }
        public Guid UserStoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TechnicalSummary { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public decimal EstimatedHours { get; set; }
        public string EffortSize { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Guid? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
    }
}
