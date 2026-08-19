using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Tasks
{
    public sealed class TaskStatusChangedDto
    {
        public Guid ProjectId { get; set; }

        public Guid SprintId { get; set; }

        public Guid TaskId { get; set; }

        public string TaskTitle { get; set; } = string.Empty;

        public TaskItemStatus PreviousStatus { get; set; }

        public TaskItemStatus NewStatus { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}
