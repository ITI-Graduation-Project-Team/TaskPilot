using TaskPilot.Models.Common;
using System;

namespace TaskPilot.Models.Entities
{
    public class ProjectChatMessage : AuditableEntity<Guid>
    {
        public Guid SessionId { get; set; }
        public ProjectChatSession Session { get; set; } = null!;

        /// <summary>
        /// User or Assistant
        /// </summary>
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
        
        public int SequenceIndex { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
