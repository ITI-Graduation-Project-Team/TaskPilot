using System.Collections.Generic;

namespace TaskPilot.AI.Models.Requirements
{
    public class RequirementCompletenessReport
    {
        public const int ConfirmationThreshold = 85;

        public int OverallCompleteness { get; set; }
        
        public string Readiness { get; set; } = string.Empty;
        
        public List<string> BlockingCategories { get; set; } = new();
        
        public int HighPriorityQuestions { get; set; }
        
        public int MediumQuestions { get; set; }
        
        public int LowQuestions { get; set; }
        
        public List<string> MissingCriticalAreas { get; set; } = new();
        
        public string ReadinessRecommendation { get; set; } = string.Empty;
        
        public List<string> BlockingFactors { get; set; } = new();
        
        public int EstimatedCompletenessAfterPendingQuestions { get; set; }
        
        public bool ReadyForFinalization { get; set; }

        public bool MeetsConfirmationThreshold() =>
            OverallCompleteness >= ConfirmationThreshold;
    }
}
