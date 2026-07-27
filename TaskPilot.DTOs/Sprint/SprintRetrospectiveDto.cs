namespace TaskPilot.DTOs.Sprint
{
    public class SprintRetrospectiveDto
    {
        public Guid SprintId { get; set; }
        public string SprintTitleEn { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }

        public SprintMetricsDto Metrics { get; set; } = new();
        public SprintAnalysisDto Analysis { get; set; } = new();
        public List<SprintImprovementDto> Improvements { get; set; } = new();
    }

    public class SprintMetricsDto
    {
        public double CompletionRate { get; set; }
        public double VelocityRatio { get; set; }
        public decimal TotalEstimatedHours { get; set; }
        public decimal TotalActualHours { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int UnfinishedTasks { get; set; }
        public string PerformanceLabel { get; set; } = string.Empty;
        // "OnTrack" | "SlightlyDelayed" | "Delayed" | "Blocked"
        public List<DeveloperMetricDto> DeveloperMetrics { get; set; } = new();
    }

    public class DeveloperMetricDto
    {
        public string FullName { get; set; } = string.Empty;
        public double CompletionRate { get; set; }
        public double VelocityRatio { get; set; }
        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public string PerformanceLabel { get; set; } = string.Empty;
        // "Fast" | "OnTime" | "SlightlyOver" | "Over"
    }

    public class SprintAnalysisDto
    {
        public string SummaryEn { get; set; } = string.Empty;
        public string SummaryAr { get; set; } = string.Empty;
        public List<string> WhatWentWellEn { get; set; } = new();
        public List<string> WhatWentWellAr { get; set; } = new();
        public List<string> WhatNeedsImprovementEn { get; set; } = new();
        public List<string> WhatNeedsImprovementAr { get; set; } = new();
        public List<string> RiskSignalsEn { get; set; } = new();
        public List<string> RiskSignalsAr { get; set; } = new();
    }

    public class SprintImprovementDto
    {
        public string Category { get; set; } = string.Empty;
        // "Capacity" | "Assignment" | "Estimation" | "Process" | "Technical"

        public string RecommendationEn { get; set; } = string.Empty;
        public string RecommendationAr { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;
        // "High" | "Medium" | "Low"

        public string ActionType { get; set; } = string.Empty;
        // "ReduceCapacity" | "ReassignDeveloper" | "AddBuffer"
        // "ReduceTaskCount" | "SplitLargeTasks" | "None"

        public Guid? TargetEmployeeId { get; set; }
        public double? SuggestedAdjustment { get; set; }
    }
}
