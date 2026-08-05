using System;

namespace TaskPilot.Services
{
    public class SprintCapacityResult
    {
        public decimal TargetSprintHours { get; set; }
        public string ExplanationEn { get; set; } = string.Empty;
        public string ExplanationAr { get; set; } = string.Empty;
    }
}
