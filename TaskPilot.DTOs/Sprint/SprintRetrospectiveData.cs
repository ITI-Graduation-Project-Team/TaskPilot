namespace TaskPilot.DTOs.Sprint
{
    public class SprintRetrospectiveData
    {
        // Sprint Overview
        public Guid SprintId { get; set; }
        public string SprintTitleEn { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int ActualDurationDays { get; set; }
        public int PlannedDurationDays { get; set; }

        // Task Metrics
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }     // moved back to ToDo at close
        public int NotStartedTasks { get; set; }
        public double CompletionRate { get; set; }   // CompletedTasks / TotalTasks * 100

        // Hours Metrics
        public decimal TotalEstimatedHours { get; set; }
        public decimal TotalActualHours { get; set; }
        public double VelocityRatio { get; set; }    // Actual / Estimated
        // < 1.0 = faster than estimated
        // > 1.0 = slower than estimated
        // 1.0 = perfect

        // Per-Developer Breakdown
        public List<DeveloperRetrospectiveData> DeveloperBreakdowns { get; set; } = new();

        // Unfinished Work
        public List<UnfinishedTaskData> UnfinishedTasks { get; set; } = new();

        // Feature Completeness Index (Partially Completed Stories: 0% < Completion < 100%)
        public List<PartiallyCompletedStoryData> PartiallyCompletedStories { get; set; } = new();
    }

    public class PartiallyCompletedStoryData
    {
        public Guid UserStoryId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int RemainingTasks { get; set; }
        public double CompletionPercentage { get; set; }
    }

    public class DeveloperRetrospectiveData
    {
        public Guid EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int AssignedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; }
        public double VelocityRatio { get; set; }
        public double CompletionRate { get; set; }
        public List<string> CompletedTaskTypes { get; set; } = new();
        // "Technical" | "NonTechnical"
    }

    public class UnfinishedTaskData
    {
        public Guid TaskId { get; set; }
        public Guid? UserStoryId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public decimal EstimatedHours { get; set; }
        public string Reason { get; set; } = string.Empty;
        // "NotStarted" | "InProgress"
        public string AssigneeName { get; set; } = string.Empty;
    }
}
