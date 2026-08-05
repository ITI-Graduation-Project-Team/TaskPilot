using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Projects
{
    public class WbsDto
    {
        public Guid ProjectId { get; set; }
        public IEnumerable<WbsUserStoryDto> UserStories { get; set; } = new List<WbsUserStoryDto>();
    }

    public class WbsUserStoryDto
    {
        public Guid Id { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string? TitleAr { get; set; }
        public string Priority { get; set; } = string.Empty;
        public Guid? SprintId { get; set; }
        public IEnumerable<WbsTaskDto>? Tasks { get; set; }
    }

    public class WbsTaskDto
    {
        public Guid Id { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string EffortSize { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal EstimatedHours { get; set; }
        public Guid? SprintId { get; set; }
    }
}
