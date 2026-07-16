using System.Collections.Generic;

namespace TaskPilot.AI.Models.Requirements
{
    public class RequirementValidationResult
    {
        public int ValidationScore { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string BusinessReadiness { get; set; } = string.Empty;
    }
}
