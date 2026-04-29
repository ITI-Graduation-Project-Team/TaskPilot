using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Sprint : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? SprintGoalEn { get; set; }
        public string? SprintGoalAr { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }


        public SprintStatus Status { get; set; } = SprintStatus.Planned;

        public ICollection<UserStory> UserStories { get; set; } = new List<UserStory>();
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
