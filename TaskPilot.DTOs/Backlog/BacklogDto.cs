using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Backlog
{
    public class BacklogDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public List<UserStoryDto> UserStories { get; set; } = new();
    }
}
