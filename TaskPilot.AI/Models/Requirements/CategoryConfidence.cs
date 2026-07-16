using System.Collections.Generic;

namespace TaskPilot.AI.Models.Requirements
{
    /// <summary>
    /// Raw AI output per category before mapping to RequirementConfidenceScore.
    /// All new fields are additive — existing consumers compile unchanged.
    /// </summary>
    public class CategoryConfidence
    {
        public string Category { get; set; } = string.Empty;

        /// <summary>0–100 coverage score for this category.</summary>
        public int Score { get; set; }

        /// <summary>"Covered" | "PartiallyCovered" | "Missing"</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Short text quoted from the BRD that was the basis for this score
        /// (kept for backward-compat; Evidence carries the richer citation).
        /// </summary>
        public string? ExtractedValue { get; set; }

        // ── Enriched fields (additive) ──────────────────────────────────────

        /// <summary>
        /// Human-readable explanation of why this score was assigned.
        /// E.g. "Release date exists but implementation milestones are absent."
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Exact sentence, section name, or requirement ID from the BRD that
        /// supports the score.  Never generated — always extracted.
        /// </summary>
        public string Evidence { get; set; } = string.Empty;

        /// <summary>
        /// Specific sub-items that are absent and drive gap questions.
        /// E.g. ["Milestones", "Project Phases", "Sprint Schedule"]
        /// </summary>
        public List<string> MissingItems { get; set; } = new();
    }
}
