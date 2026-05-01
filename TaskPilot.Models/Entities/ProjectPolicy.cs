using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class ProjectPolicy : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string ContentEn { get; set; } = string.Empty;
        public string ContentAr { get; set; } = string.Empty;
        public int VersionNumber { get; set; } = 1;
    }
}
