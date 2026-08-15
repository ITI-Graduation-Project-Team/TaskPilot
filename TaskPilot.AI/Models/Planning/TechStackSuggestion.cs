namespace TaskPilot.AI.Models.Planning
{
    public class TechStackSuggestion
    {
        /// <summary>
        /// Stack recommended based on the actual skills of available
        /// employees. The PM can start building immediately with this stack.
        /// </summary>
        public RecommendedStack PrimaryStack { get; set; } = new();

        /// <summary>
        /// Stack that is optimal for the project requirements regardless
        /// of current team composition.
        /// </summary>
        public RecommendedStack IdealStack { get; set; } = new();

        /// <summary>
        /// Structured capabilities required by the ideal stack but missing
        /// or under-covered by the current project team.
        /// </summary>
        public System.Collections.Generic.List<SkillGap> GapAnalysis { get; set; } = new();

        /// <summary>
        /// Target platforms detected from requirements.
        /// Values: "Web" | "Mobile" | "Desktop" | "API"
        /// </summary>
        public System.Collections.Generic.List<string> PlatformTargets { get; set; } = new();

        /// <summary>
        /// High-level project classification.
        /// Values: "ERP" | "SaaS" | "MobileApp" | "API" | "Portal" | "Other"
        /// </summary>
        public string ProjectType { get; set; } = string.Empty;
    }

    public class RecommendedStack
    {
        public string Description { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> TechStack { get; set; } = new();
        public string Reasoning { get; set; } = string.Empty;
    }

    public class SkillGap
    {
        public string Skill { get; set; } = string.Empty;
        public string Technology { get; set; } = string.Empty;
        public string GapType { get; set; } = "Unclassified";
        public string Severity { get; set; } = "Medium";
        public string? RequiredLevel { get; set; }
        public string? AvailableLevel { get; set; }
        public int? RequiredCount { get; set; }
        public int AvailableCount { get; set; }
        public decimal AvailableFte { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }
}
