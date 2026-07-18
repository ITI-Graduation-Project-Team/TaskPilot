using TaskPilot.Models.Common;
using System;
using System.Collections.Generic;

namespace TaskPilot.Models.Entities
{
    public class ProjectChatSession : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string? BrdExtractedText { get; set; }

        public ICollection<ProjectChatMessage> Messages { get; set; } = new List<ProjectChatMessage>();
    }
}
