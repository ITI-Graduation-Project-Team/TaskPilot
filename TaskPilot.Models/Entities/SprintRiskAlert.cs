using System;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class SprintRiskAlert : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint Sprint { get; set; } = null!;
        public SprintRiskType RiskType { get; set; }
        public RiskSeverity Severity { get; set; }
        
        public Guid? AffectedTaskId { get; set; }
        public TaskItem? AffectedTask { get; set; }
        
        public Guid? AffectedEmployeeId { get; set; }
        public Employee? AffectedEmployee { get; set; }
        
        public string MessageEn { get; set; } = string.Empty;
        public string MessageAr { get; set; } = string.Empty;
        
        public DateTime LastDetectedAt { get; set; }
        public bool IsDismissed { get; set; } = false;
    }
}
