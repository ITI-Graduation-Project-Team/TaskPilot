using System.Collections.Generic;

namespace TaskPilot.AI.Models.Planning
{
    public class TechStackSuggestion
    {
        /// <summary>
        /// Recommended technologies.
        /// Example: ["ASP.NET Core 8", "React 18", "SQL Server", "Redis"]
        /// </summary>
        public List<string> TechStack { get; set; } = new();

        /// <summary>
        /// Target platforms detected from requirements.
        /// Possible values: "Web", "Mobile", "Desktop", "API"
        /// </summary>
        public List<string> PlatformTargets { get; set; } = new();

        /// <summary>
        /// High-level project classification.
        /// Possible values: "ERP", "SaaS", "MobileApp", "API", "Portal", "Other"
        /// </summary>
        public string ProjectType { get; set; } = string.Empty;

        /// <summary>
        /// Short explanation of why this stack was recommended.
        /// Example: "React chosen for web UI, Flutter for mobile due to
        /// cross-platform requirement. SQL Server for HIPAA compliance."
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;
    }
}
