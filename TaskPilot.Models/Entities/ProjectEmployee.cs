using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class ProjectEmployee
    {
        public Guid ProjectId { get; set; }
        public Guid EmployeeId { get; set; }

        public Project Project { get; set; } = null!;
        public Employee Employee { get; set; } = null!;
        public ProjectRole Role { get; set; }

    }
}
