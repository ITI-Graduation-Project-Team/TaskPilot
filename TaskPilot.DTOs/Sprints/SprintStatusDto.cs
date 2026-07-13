using System;

namespace TaskPilot.DTOs.Sprints
{
    public class SprintStatusDto
    {
        public Guid SprintId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
