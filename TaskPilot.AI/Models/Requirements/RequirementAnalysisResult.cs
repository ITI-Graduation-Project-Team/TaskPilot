using System.Collections.Generic;

namespace TaskPilot.AI.Models.Requirements
{
    // ── Rich gap-question object produced by the enriched AI prompt ─────────────
    public class GapQuestion
    {
        /// <summary>The question text shown to the PM.</summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>BRD category this question addresses (matches CategoryConfidence.Category).</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>"Critical" | "High" | "Medium" | "Low" — driven by business impact.</summary>
        public string Priority { get; set; } = "Medium";

        /// <summary>Why this question is important (not a repeat of the question text).</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Sub-items from CategoryConfidence.MissingItems that this question targets.</summary>
        public List<string> MissingItems { get; set; } = new();

        /// <summary>Plain-English statement of business risk if left unanswered.</summary>
        public string BusinessImpact { get; set; } = string.Empty;

        /// <summary>
        /// Estimated improvement to OverallCompletenessScore if answered (0–100 integer).
        /// </summary>
        public int EstimatedEffectOnCompleteness { get; set; }
    }

    // ── Top-level AI analysis result ──────────────────────────────────
    public class RequirementAnalysisResult
    {
        public ExtractedRequirements ExtractedRequirements { get; set; } = new();

        public List<CategoryConfidence> ConfidenceScores { get; set; } = new();

        /// <summary>
        /// Rich gap questions produced by the enriched prompt.
        /// Use GapQuestionsAsStrings for backward-compatible plain-string access.
        /// </summary>
        public List<GapQuestion> GapQuestions { get; set; } = new();

        // ── Overall analysis ──────────────────────────────────────────

        /// <summary>
        /// Weighted completeness score 0–100, reasoned by the AI across all
        /// categories — NOT a simple average.
        /// </summary>
        public int OverallCompletenessScore { get; set; }

        /// <summary>
        /// True only when the AI judges all critical categories are
        /// sufficiently covered for project planning to begin.
        /// </summary>
        public bool FinalizeReadiness { get; set; }

        /// <summary>Estimated readiness as integer percentage (mirrors OverallCompletenessScore).</summary>
        public int EstimatedReadiness { get; set; }

        /// <summary>
        /// Plain-text recommendation for the PM, e.g.
        /// "Timeline and compliance definitions are incomplete. Two high-priority
        ///  questions must be answered before planning."
        /// </summary>
        public string Recommendation { get; set; } = string.Empty;

        /// <summary>
        /// Comprehensive completeness report containing dynamic analysis.
        /// </summary>
        public RequirementCompletenessReport RequirementCompletenessReport { get; set; } = new();

        // ── Backward-compatible helpers ─────────────────────────────────

        /// <summary>
        /// Plain strings for code that consumed the old List&lt;string&gt; shape.
        /// </summary>
        public IReadOnlyList<string> GapQuestionsAsStrings =>
            GapQuestions.ConvertAll(q => q.Question);
    }
}
