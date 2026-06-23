using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class UserStory : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        /// <summary>
        /// Null when the UserStory has been generated but not yet assigned
        /// to a Sprint. This is by design — not a data integrity error.
        /// Gets populated when the PM approves Sprint assignment (Sprint 5e).
        /// </summary>
        public Guid? SprintId { get; set; }
        public Sprint? Sprint { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public StoryPriority Priority { get; set; }
        public StoryStatus Status { get; set; } = StoryStatus.ToDo;
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>(); //on cascade delete no action
    }
}
