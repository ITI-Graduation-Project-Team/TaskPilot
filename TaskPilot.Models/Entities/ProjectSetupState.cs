using System.ComponentModel.DataAnnotations;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class ProjectSetupState : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public TechStackSetupStatus TechStackStatus { get; set; } = TechStackSetupStatus.NotStarted;
        public string? TechStackSuggestionJson { get; set; }
        public string? TechStackError { get; set; }

        public BackgroundSetupStatus WbsStatus { get; set; } = BackgroundSetupStatus.NotStarted;
        public string? WbsJobId { get; set; }
        public int WbsAttemptCount { get; set; }
        public int UserStoriesCreated { get; set; }
        public int TasksCreated { get; set; }
        public DateTime? WbsStartedAt { get; set; }
        public DateTime? WbsCompletedAt { get; set; }
        public string? WbsError { get; set; }

        public BackgroundSetupStatus SkillsStatus { get; set; } = BackgroundSetupStatus.NotStarted;
        public string? SkillsJobId { get; set; }
        public int SkillsAttemptCount { get; set; }
        public int TasksProcessed { get; set; }
        public int TasksEnriched { get; set; }
        public int TasksSkipped { get; set; }
        public int SkillsCreated { get; set; }
        public DateTime? SkillsStartedAt { get; set; }
        public DateTime? SkillsCompletedAt { get; set; }
        public string? SkillsError { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
