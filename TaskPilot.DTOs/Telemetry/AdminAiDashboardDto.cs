using System.Collections.Generic;

namespace TaskPilot.DTOs.Telemetry
{
    public class AdminAiDashboardDto
    {
        public int TotalOperations { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCostUsd { get; set; }
        public long AverageResponseTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, int> ModelUsageCounts { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> OperationTypeCounts { get; set; } = new Dictionary<string, int>();
    }
}
