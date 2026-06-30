using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class UserStory : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint Sprint { get; set; } = null!;
        public string TitleEn { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public StoryPriority Priority { get; set; }
        public StoryStatus Status { get; set; } = StoryStatus.ToDo;
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
