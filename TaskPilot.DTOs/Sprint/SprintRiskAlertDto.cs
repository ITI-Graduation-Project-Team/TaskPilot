using System;

namespace TaskPilot.DTOs.Sprint
{
    public class SprintRiskAlertDto
    {
        public Guid Id { get; set; }
        public string RiskType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string MessageEn { get; set; } = string.Empty;
        public string MessageAr { get; set; } = string.Empty;
        
        public Guid? AffectedTaskId { get; set; }
        public string? AffectedTaskTitle { get; set; }
        
        public Guid? AffectedEmployeeId { get; set; }
        public string? AffectedEmployeeName { get; set; }
        
        public DateTime DetectedAt { get; set; }
        public bool IsDismissed { get; set; }
    }
}
