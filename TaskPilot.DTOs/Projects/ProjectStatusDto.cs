using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Projects
{
    public class ProjectStatusDto
    {
        public Guid ProjectId { get; set; }
        public ProjectStatus Status { get; set; }
    }
}
