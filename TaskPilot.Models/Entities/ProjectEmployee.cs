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

        // Set to false when the employee is deactivated.
        // Preserves the historical record while excluding the employee
        // from active-member queries (e.g. sprint planning, assignment scoring).
        public bool IsActive { get; set; } = true;

        public decimal AllocationPercentage { get; set; } = 100.0m;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
