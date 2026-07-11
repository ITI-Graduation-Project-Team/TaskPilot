using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class Project : AuditableEntity<Guid>
    {
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public Guid ManagerId { get; set; }
        public Guid CompanyId { get; set; }
        public ProjectManager Manager { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
        public ICollection<Policy> Policies { get; set; } = new List<Policy>();
        public ICollection<ProjectEmployee> ProjectEmployees { get; set; } = new List<ProjectEmployee>();
        public ProjectStatus Status { get; set; }

        /// <summary>
        /// Duration of every Sprint in this Project.
        /// Default Agile duration = 14 days.
        /// Used when generating future Sprint schedules.
        /// </summary>
        public int SprintDurationInDays { get; set; } = 14;

        /// <summary>
        /// Target workload for a Sprint.
        /// Used only during Sprint Planning.
        /// The AI should attempt to build a Sprint close to this value.
        /// Default = 80 hours.
        /// </summary>
        public decimal TargetSprintHours { get; set; } = 80;

        /// <summary>
        /// Technologies recommended by TechStackAdvisorAgent and approved by PM.
        /// Example: ["ASP.NET Core 8", "React 18", "SQL Server", "Redis", "Flutter"]
        /// Populated after project creation via /api/projects/{id}/tech-stack/confirm
        /// </summary>
        public List<string> TechStack { get; set; } = new();

        /// <summary>
        /// Target platforms for this project.
        /// Example: ["Web", "Mobile", "Desktop"]
        /// Used by WBSGenerationAgent to generate platform-specific UserStories.
        /// </summary>
        public List<string> PlatformTargets { get; set; } = new();

        /// <summary>
        /// High-level project type used to guide WBS generation.
        /// Example: "ERP" | "SaaS" | "MobileApp" | "API" | "Portal" | "Other"
        /// </summary>
        public string ProjectType { get; set; } = string.Empty;
        
        public RequirementsSnapshot? RequirementsSnapshot { get; set; }
        public Guid? RequirementsSessionId { get; set; }
        public List<Guid> DocumentIds { get; set; } = new List<Guid>();
        public ICollection<UserStory> UserStories { get; set; } = new List<UserStory>();
    }
}