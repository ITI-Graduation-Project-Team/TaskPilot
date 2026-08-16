using System;
using System.Collections.Generic;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.DTOs.Backlog
{
    public class PaginatedBacklogDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public PagedResult<UserStoryDto> UserStories { get; set; } = new();
    }
}
