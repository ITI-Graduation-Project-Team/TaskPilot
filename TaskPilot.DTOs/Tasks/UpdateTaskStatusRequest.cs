using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Tasks
{
    public class UpdateTaskStatusRequest
    {
        public TaskItemStatus Status { get; set; }
        public decimal? ActualHours { get; set; }
    }
}
