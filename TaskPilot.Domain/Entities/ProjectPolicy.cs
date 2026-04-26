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
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string ContentEn { get; set; } = string.Empty;
        public string ContentAr { get; set; } = string.Empty;
        public int VersionNumber { get; set; } = 1;
    }
}
