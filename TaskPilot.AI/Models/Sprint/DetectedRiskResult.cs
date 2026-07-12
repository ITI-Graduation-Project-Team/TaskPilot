using System;
using System.Collections.Generic;

namespace TaskPilot.AI.Models.Sprint
{
    public class DetectedRiskResult
    {
        public List<DetectedRisk> Risks { get; set; } = new();
    }

    public class DetectedRisk
    {
        public string RiskType { get; set; } = string.Empty; // matches SprintRiskType enum name
        public string Severity { get; set; } = string.Empty; // matches RiskSeverity enum name
        public Guid? AffectedTaskId { get; set; }
        public Guid? AffectedEmployeeId { get; set; }
        public string MessageEn { get; set; } = string.Empty;
        public string MessageAr { get; set; } = string.Empty;
    }
}
