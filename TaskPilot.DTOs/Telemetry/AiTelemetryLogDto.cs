using System;

namespace TaskPilot.DTOs.Telemetry
{
    public class AiTelemetryLogDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public long ResponseTimeMs { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
