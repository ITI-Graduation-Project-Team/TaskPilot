using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class ProjectPolicy : AuditableEntity
    {
        public Guid ProjectId { get; set; } 
        public virtual Project Project { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int VersionNumber { get; set; } = 1;
        public bool IsActive { get; set; } = true;
    }
}
