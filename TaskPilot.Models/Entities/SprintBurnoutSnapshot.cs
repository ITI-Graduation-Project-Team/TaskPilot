using System;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class SprintBurnoutSnapshot : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint Sprint { get; set; } = null!;
        
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        
        public int BurnoutScore { get; set; }      // 0-100
        public int WorkloadScore { get; set; }     // 0-100
        public int PaceScore { get; set; }         // 0-100 (Replaces Erratic Hours)
        public int EngagementScore { get; set; }   // 0-100
        
        public string RiskLevel { get; set; } = string.Empty; // "Healthy", "AtRisk", "High"
        public string TrendDirection { get; set; } = string.Empty; // "+rising", "stable", "-improving"
        
        public DateTime AnalyzedAt { get; set; }
    }
}
