using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class TaskAttachment : AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;     
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public TaskItem Task { get; set; } = null!;
    }
}
