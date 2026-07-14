using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Tasks
{
    public class TaskStatusUpdateResult
    {
        public Guid TaskId { get; set; }

        public string TitleEn { get; set; } = string.Empty;

        public TaskItemStatus PreviousStatus { get; set; }

        public TaskItemStatus NewStatus { get; set; }

        public decimal EstimatedHours { get; set; }

        public decimal ActualHours { get; set; }
    }
}
