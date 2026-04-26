using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class UserStory : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Sprint? Sprint { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public StoryPriority Priority { get; set; }
        public StoryStatus Status { get; set; } = StoryStatus.ToDo;
        public ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
