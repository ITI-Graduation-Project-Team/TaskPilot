using System;
using System.Collections.Generic;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Tasks
{
    public class MyTaskDto
    {
        public Guid TaskId { get; set; }

        public string TitleEn { get; set; } = string.Empty;

        public string TitleAr { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }

        public string? DescriptionAr { get; set; }

        public string? AcceptanceCriteriaEn { get; set; }

        public string? AcceptanceCriteriaAr { get; set; }

        public TaskItemStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public EffortSize EffortSize { get; set; }

        public TaskType Type { get; set; }

        public decimal EstimatedHours { get; set; }

        public decimal ActualHours { get; set; }

        public string UserStoryTitleEn { get; set; } = string.Empty;

        public string UserStoryTitleAr { get; set; } = string.Empty;

        public List<string> RequiredSkills { get; set; } = new();
    }
}
