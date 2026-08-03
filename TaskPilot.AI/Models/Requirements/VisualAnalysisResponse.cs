using System.Collections.Generic;

namespace TaskPilot.AI.Models.Requirements
{
    public class VisualAnalysisResponse
    {
        public string DiagramType { get; set; } = string.Empty;
        public string SummaryDescription { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        public List<DiagramEntity> Entities { get; set; } = new();
        public List<VisualRequirementDto> ExtractedRequirements { get; set; } = new();
    }

    public class DiagramEntity
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Attributes { get; set; } = new();
        public List<string> Relationships { get; set; } = new();
    }

    public class VisualRequirementDto
    {
        public string Text { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}
