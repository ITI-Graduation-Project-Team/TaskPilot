using System.Linq;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Enums;
using System.Collections.Generic;

namespace TaskPilot.AI.Services.Requirements
{
    public interface IRequirementReadinessEvaluator
    {
        RequirementCompletenessReport Evaluate(RequirementSession session);
    }

    public class RequirementReadinessEvaluator : IRequirementReadinessEvaluator
    {
        public RequirementCompletenessReport Evaluate(RequirementSession session)
        {
            var report = new RequirementCompletenessReport();
            
            if (session.ConfidenceScores == null || !session.ConfidenceScores.Any())
            {
                report.ReadyForFinalization = false;
                report.BlockingFactors.Add("No confidence scores available. Please upload a BRD or provide initial requirements.");
                report.Readiness = "Needs Initial Requirements";
                return report;
            }

            var blockingCategories = session.ConfidenceScores
                .Where(c => c.Status == "Missing" || c.Score < 40)
                .Select(c => c.Category)
                .ToList();

            var pendingQuestions = session.QuestionPool.Where(q => !q.IsAnswered).ToList();
            
            report.HighPriorityQuestions = pendingQuestions.Count(q => q.Priority == QuestionPriority.Critical || q.Priority == QuestionPriority.High);
            report.MediumQuestions = pendingQuestions.Count(q => q.Priority == QuestionPriority.Medium);
            report.LowQuestions = pendingQuestions.Count(q => q.Priority == QuestionPriority.Low);
            
            report.BlockingCategories = blockingCategories;
            
            var missingCritical = session.ConfidenceScores
                .Where(c => (c.Category == "BusinessGoals" || c.Category == "Scale" || c.Category == "UserRoles") && (c.Status == "Missing" || c.Score < 40))
                .Select(c => c.Category)
                .ToList();
                
            report.MissingCriticalAreas = missingCritical;

            // Deterministic Completeness Score (0-100)
            int totalCategories = session.ConfidenceScores.Count;
            int totalScore = session.ConfidenceScores.Sum(c => c.Score);
            report.OverallCompleteness = totalCategories > 0 ? totalScore / totalCategories : 0;
            
            // Re-evaluate Readiness deterministically
            if (report.OverallCompleteness < 85)
            {
                report.BlockingFactors.Add($"Overall completeness is {report.OverallCompleteness}%, which is below the required 85%.");
            }
            
            if (report.HighPriorityQuestions > 0)
            {
                report.BlockingFactors.Add($"{report.HighPriorityQuestions} high-priority questions remain unanswered.");
            }
            
            if (missingCritical.Any())
            {
                report.BlockingFactors.Add($"Critical categories are missing or insufficient: {string.Join(", ", missingCritical)}");
            }

            report.ReadyForFinalization = !report.BlockingFactors.Any();
            report.Readiness = report.ReadyForFinalization ? "Ready For Finalization" : "Needs Clarification";
            report.ReadinessRecommendation = report.ReadyForFinalization 
                ? "The requirements are sufficient for sprint planning. You may finalize."
                : "Please answer the pending questions to improve completeness before finalizing.";

            // If we have AI-generated completeness report, we can blend it (e.g., keep the AI's estimated improvement)
            if (session.RequirementCompletenessReport != null)
            {
                report.EstimatedCompletenessAfterPendingQuestions = session.RequirementCompletenessReport.EstimatedCompletenessAfterPendingQuestions;
            }
            else
            {
                report.EstimatedCompletenessAfterPendingQuestions = report.OverallCompleteness + (pendingQuestions.Count * 5); // Fallback deterministic estimate
                if (report.EstimatedCompletenessAfterPendingQuestions > 100)
                    report.EstimatedCompletenessAfterPendingQuestions = 100;
            }

            return report;
        }
    }
}
