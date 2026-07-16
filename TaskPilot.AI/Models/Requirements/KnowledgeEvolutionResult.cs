using System;

namespace TaskPilot.AI.Models.Requirements
{
    public class KnowledgeEvolutionResult
    {
        /// <summary>Add, Modify, Conflict, Answer, Comment, Clarification, None</summary>
        public string Intent { get; set; } = "None";
        public Guid? TargetRequirementId { get; set; }
        public string ProposedText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }
}
