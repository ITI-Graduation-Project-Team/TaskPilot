using System;

namespace TaskPilot.DTOs.Telemetry
{
    public class ProjectMemberAiUsageDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // e.g. Employee or PM
        public int TotalOperations { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCostUsd { get; set; }
    }
}
