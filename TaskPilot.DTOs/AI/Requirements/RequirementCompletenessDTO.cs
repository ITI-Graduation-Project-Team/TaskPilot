using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.Requirements
{
    public class RequirementCompletenessDTO
    {
        public int OverallCompleteness { get; set; }
        public ReadinessDTO Readiness { get; set; } = new();
        public List<string> BlockingCategories { get; set; } = new();
        public QuestionImpactDTO QuestionImpact { get; set; } = new();
        public List<string> MissingCriticalAreas { get; set; } = new();
        public string ReadinessRecommendation { get; set; } = string.Empty;
        public List<BlockingFactorsDTO> BlockingFactors { get; set; } = new();
        public int EstimatedCompletenessAfterPendingQuestions { get; set; }
        public bool ReadyForFinalization { get; set; }
        public bool IsComplete => OverallCompleteness >= 100;
    }

    public class ReadinessDTO
    {
        public string Status { get; set; } = string.Empty;
    }

    public class BlockingFactorsDTO
    {
        public string Factor { get; set; } = string.Empty;
    }

    public class QuestionImpactDTO
    {
        public int HighPriorityQuestions { get; set; }
        public int MediumPriorityQuestions { get; set; }
        public int LowPriorityQuestions { get; set; }
    }

    public class CategoryEvaluationDTO
    {
        public string Category { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public List<string> MissingItems { get; set; } = new();
    }
}
