using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class CalenderTaskDetailsDto
    {
        public Guid Id { get; set; }
        public Guid RelatedTaskId {  get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string TaskType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DurationInMinutes { get; set; }

        public string? Status { get; set; }
        public DateTime? DueDate { get; set; }
        public string? RelatedSprint { get; set; }
        public string? ProjectName { get; set; }
        public string? AiQuickSummary { get; set; }
    }
}
