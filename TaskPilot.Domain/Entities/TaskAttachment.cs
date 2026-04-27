using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class TaskAttachment : AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public Task Task { get; set; } = null!;
    }
}
