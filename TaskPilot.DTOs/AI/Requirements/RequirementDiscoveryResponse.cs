using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.Requirements
{
    public class RequirementDiscoveryResponse
    {
        public Guid SessionId { get; set; }
        public string WorkflowState { get; set; } = string.Empty;
        public int DocumentsProcessed { get; set; }
        public bool ConversationUpdated { get; set; }
        
        public ExtractedRequirementsDTO Requirements { get; set; } = new();
        public RequirementCompletenessDTO CompletenessReport { get; set; } = new();
        public List<ClarificationQuestionDTO> PendingQuestions { get; set; } = new();
        
        public CoverageDTO Coverage { get; set; } = new();
        public ConfidenceSummaryDTO ConfidenceSummary { get; set; } = new();
        public List<DocumentSummaryDTO> Documents { get; set; } = new();
        public AnalysisSummaryDTO AnalysisSummary { get; set; } = new();
        public RequirementValidationResultDTO? ValidationResult { get; set; }
        
        public string NextRecommendedAction { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
    }

    public class ExtractedRequirementsDTO
    {
        public List<string> BusinessRequirements { get; set; } = new();
        public List<string> TechnicalRequirements { get; set; } = new();
        public List<string> Constraints { get; set; } = new();
        public List<string> Integrations { get; set; } = new();
        public List<string> ScaleRequirements { get; set; } = new();
        
        public List<RequirementItemDTO> EnrichedRequirements { get; set; } = new();
    }

    public class RequirementItemDTO
    {
        public string Text { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Confidence { get; set; }
        public string Evidence { get; set; } = string.Empty;
    }

    public class ClarificationQuestionDTO
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public List<string> MissingItems { get; set; } = new();
        public string BusinessImpact { get; set; } = string.Empty;
        public int EstimatedEffectOnCompleteness { get; set; }
    }

    public class CoverageDTO
    {
        public List<string> Covered { get; set; } = new();
        public List<string> Partial { get; set; } = new();
        public List<string> Missing { get; set; } = new();
    }

    public class ConfidenceSummaryDTO
    {
        public int CoveredCategories { get; set; }
        public int PartialCategories { get; set; }
        public int MissingCategories { get; set; }
        public int AverageConfidence { get; set; }
        public int HighestConfidence { get; set; }
        public int LowestConfidence { get; set; }
    }

    public class DocumentSummaryDTO
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public int ChunkCount { get; set; }
        public int VisualAssetCount { get; set; }
    }

    public class AnalysisSummaryDTO
    {
        public int DocumentsAnalyzed { get; set; }
        public int ConversationMessages { get; set; }
        public int QuestionsGenerated { get; set; }
        public int QuestionsResolved { get; set; }
    }

    public class RequirementValidationResultDTO
    {
        public int ValidationScore { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string BusinessReadiness { get; set; } = string.Empty;
    }
}
