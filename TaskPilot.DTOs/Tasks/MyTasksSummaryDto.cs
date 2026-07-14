using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Tasks
{
    public class MyTasksSummaryDto
    {
        public Guid SprintId { get; set; }
        public string SprintTitleEn { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public int TotalTasks { get; set; }
        public int ToDoCount { get; set; }
        public int InProgressCount { get; set; }
        public int DoneCount { get; set; }
        public decimal TotalEstimatedHours { get; set; }
        public decimal TotalActualHours { get; set; }
        public decimal CompletionPercentage { get; set; }
        
        public List<MyTaskDto> Tasks { get; set; } = new();
    }
}
