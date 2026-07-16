using System;
using System.Collections.Generic;

namespace TaskPilot.AI.Models.Requirements
{
    public class RequirementIdentity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OriginalText { get; set; } = string.Empty;
        public string NormalizedText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Confidence { get; set; } = 100;
        public int Version { get; set; } = 1;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public List<string> Sources { get; set; } = new();
        public List<string> Evidence { get; set; } = new();
        public bool IsConflicting { get; set; }
        public string ConflictReason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
