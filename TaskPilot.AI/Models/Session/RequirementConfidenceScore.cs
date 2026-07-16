using System.Collections.Generic;

namespace TaskPilot.AI.Models.Session
{
    public class RequirementConfidenceScore
    {
        public string Category { get; set; } = string.Empty;

        /// <summary>0–100 coverage score for this category.</summary>
        public int Score { get; set; }

        /// <summary>"Covered" | "PartiallyCovered" | "Missing"</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Short text from the BRD that was the basis for this score
        /// (backward-compat; Evidence carries the richer citation).
        /// </summary>
        public string? ExtractedValue { get; set; }

        // ── Enriched fields (additive) ──────────────────────────────────────

        /// <summary>Why this score was assigned.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Exact sentence/section/ID from the BRD that supports the score.</summary>
        public string Evidence { get; set; } = string.Empty;

        /// <summary>Specific sub-items that are absent for this category.</summary>
        public List<string> MissingItems { get; set; } = new();
    }
}
