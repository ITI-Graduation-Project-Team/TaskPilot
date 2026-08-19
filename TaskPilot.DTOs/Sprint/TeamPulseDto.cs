using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Sprint
{
    public class TeamPulseDto
    {
        public int TeamBurnoutRisk { get; set; }
        public SprintHealthSummaryDto Summary { get; set; } = new SprintHealthSummaryDto();
        public DashboardKpisDto Kpis { get; set; } = new DashboardKpisDto();
        public List<ActivityFeedItemDto> LiveActivity { get; set; } = new List<ActivityFeedItemDto>();
        public List<TeamPulseMemberDto> Members { get; set; } = new List<TeamPulseMemberDto>();
        public List<NeedsAttentionItemDto> NeedsAttention { get; set; } = new List<NeedsAttentionItemDto>();
        public List<SprintHealthRiskDto> Risks { get; set; } = new List<SprintHealthRiskDto>();
        public TeamPulseChartsDto Charts { get; set; } = new TeamPulseChartsDto();
    }

    public class SprintHealthSummaryDto
    {
        public string DeliveryStatus { get; set; } = "On Track";
        public int ProgressPercent { get; set; }
        public int EffortProgressPercent { get; set; }
        public int DoneTasks { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedEstimatedHours { get; set; }
        public int TotalEstimatedHours { get; set; }
        public int RemainingHours { get; set; }
        public int WorkingDaysLeft { get; set; }
        public int TeamRemainingCapacity { get; set; }
        public int CapacityUsagePercent { get; set; }
        public decimal EstimatedWorkingDaysNeeded { get; set; }
        public int SpareCapacityHours { get; set; }
        public int OverloadedCount { get; set; }
        public int UnassignedHighPriorityCount { get; set; }
        public int StuckTasksCount { get; set; }
        public int EstimateExceededCount { get; set; }
        public int ReviewTasksCount { get; set; }
    }

    public class TeamPulseMemberDto
    {
        public Guid EmployeeId { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        
        public string RiskLevel { get; set; } = string.Empty;
        public int BurnoutScore { get; set; }
        public decimal AssignedRemainingHours { get; set; }
        public decimal AvailableRemainingHours { get; set; }
        public decimal RemainingCapacityDeltaHours { get; set; }
        public decimal CompletedEstimatedHours { get; set; }
        public int UsagePercent { get; set; }
        public int WorkloadPressurePercent { get; set; }
        public int ActiveTasksCount { get; set; }
        public int HighPriorityTasksCount { get; set; }
        public int StuckTasksCount { get; set; }
        public int EstimateExceededTasksCount { get; set; }
        public int ReviewTasksCount { get; set; }
        public string LoadStatus { get; set; } = string.Empty;
        
        public RiskFactorsDto RiskFactors { get; set; } = new RiskFactorsDto();
        public string TrendDirection { get; set; } = string.Empty;
        public List<int> History { get; set; } = new List<int>();
    }

    public class RiskFactorsDto
    {
        public int Workload { get; set; }
        public int Pace { get; set; }
        public int Engagement { get; set; }
    }

    public class DashboardKpisDto
    {
        public string SprintProgressValue { get; set; } = string.Empty;
        public string SprintProgressSubtext { get; set; } = string.Empty;
        
        public int SprintVelocityValue { get; set; }
        public string SprintVelocitySubtext { get; set; } = string.Empty;
        
        public int SprintHealthValue { get; set; }
        public string SprintHealthSubtext { get; set; } = string.Empty;
        
        public int TeamBurnoutRiskValue { get; set; }
        public string TeamBurnoutRiskSubtext { get; set; } = string.Empty;
    }

    public class NeedsAttentionItemDto
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = string.Empty;
        public Guid? TaskId { get; set; }
        public Guid? EmployeeId { get; set; }
    }

    public class SprintHealthRiskDto
    {
        public string Type { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ActivityFeedItemDto
    {
        public Guid Id { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty; // e.g. "ALERT", "SUCCESS", "WARNING"
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TimeAgo { get; set; } = string.Empty; // e.g. "2m ago"
        public string AgentTag { get; set; } = string.Empty; // e.g. "Policy Agent", "Agile Coach"
    }

    public class TeamPulseChartsDto
    {
        public SprintBurndownDto Burndown { get; set; } = new SprintBurndownDto();
        public WorkloadDistributionDto Workload { get; set; } = new WorkloadDistributionDto();
        public List<TopContributorDto> TopContributors { get; set; } = new List<TopContributorDto>();
    }

    public class SprintBurndownDto
    {
        public List<string> Labels { get; set; } = new List<string>(); // Dates
        public List<int> IdealTrend { get; set; } = new List<int>();
        public List<int> ActualTrend { get; set; } = new List<int>();
    }

    public class WorkloadDistributionDto
    {
        public List<string> Labels { get; set; } = new List<string>(); // Job Titles
        public List<int> Series { get; set; } = new List<int>(); // Hours
    }

    public class TopContributorDto
    {
        public string Initials { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int CompletedHours { get; set; }
        public int CompletedTasksCount { get; set; }
    }
}
