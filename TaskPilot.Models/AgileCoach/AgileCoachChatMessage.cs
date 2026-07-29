using System;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.AgileCoach
{
    public class AgileCoachChatMessage : AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public TaskItem Task { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string Lang { get; set; } = null!;
    }
}
