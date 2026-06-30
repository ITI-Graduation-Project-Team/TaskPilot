using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Backlog
{
    public class BacklogDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectNameEn { get; set; } = string.Empty;
        public string? ProjectNameAr { get; set; }
        public List<UserStoryDto> UserStories { get; set; } = new();
    }
}
