using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Telemetry
{
    public class ProjectAiSummaryDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int TotalOperations { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCostUsd { get; set; }
        public long AverageResponseTimeMs { get; set; }
        public Dictionary<string, int> ModelUsageCounts { get; set; } = new Dictionary<string, int>();
    }
}
