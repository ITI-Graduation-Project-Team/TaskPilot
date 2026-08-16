using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Backlog
{
    public class UserStoryDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<TaskItemDto> Tasks { get; set; } = new();
    }
}
