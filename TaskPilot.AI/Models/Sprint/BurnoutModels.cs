using System;

namespace TaskPilot.AI.Models.Sprint
{
    public class EmployeeSprintBurnoutContext
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal MaxSprintCapacity { get; set; }
        public decimal AssignedHours { get; set; }
        public decimal ActualHours { get; set; }
        public int TasksAssigned { get; set; }
        public int TasksOverdue { get; set; }
        public int CommentsMade { get; set; }
        public int StatusUpdates { get; set; }
    }

    public class BurnoutRiskResult
    {
        public int BurnoutScore { get; set; }
        public int WorkloadScore { get; set; }
        public int PaceScore { get; set; }
        public int EngagementScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string TrendDirection { get; set; } = string.Empty;
    }
}
