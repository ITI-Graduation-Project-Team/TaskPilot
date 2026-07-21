using System;
using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class AiTelemetryLog : BaseEntity
    {
        public AiTelemetryLog()
        {
            Id = Guid.NewGuid();
        }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid? ProjectId { get; set; }
        public Project? Project { get; set; }

        public string OperationType { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        
        public decimal EstimatedCostUsd { get; set; }
        public long ResponseTimeMs { get; set; }
        
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
