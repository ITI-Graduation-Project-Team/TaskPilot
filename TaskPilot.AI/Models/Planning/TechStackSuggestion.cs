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
        /// Skills/technologies required by the ideal stack but missing
        /// from the current team.
        /// Example: ["Flutter developer needed", "Redis expertise missing"]
        /// </summary>
        public System.Collections.Generic.List<string> GapAnalysis { get; set; } = new();

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
}
