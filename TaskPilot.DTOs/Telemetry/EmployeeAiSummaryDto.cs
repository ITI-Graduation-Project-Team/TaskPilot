namespace TaskPilot.DTOs.Telemetry
{
    public class EmployeeAiSummaryDto
    {
        public int TotalOperations { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCostUsd { get; set; }
        public long AverageResponseTimeMs { get; set; }
    }
}
