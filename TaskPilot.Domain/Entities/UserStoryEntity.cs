using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class UserStoryEntity : AuditableEntity<Guid>
    {
        public Guid SprintId { get; private set; }

        public string Title { get; private set; }
        public string? Description { get; private set; }
        public string? AcceptanceCriteria { get; private set; }

        public StoryPriority Priority { get; private set; }
        public StoryStatus Status { get; private set; }

        // EF
        private UserStoryEntity() { }

        public UserStoryEntity(
            Guid sprintId,
            string title,
            StoryPriority priority,
            string? description = null)
        {
            Id = Guid.NewGuid();

            SprintId = sprintId;
            Title = title;
            Description = description;

            Priority = priority;
            Status = StoryStatus.ToDo;

            //SetCreated();
        }
    }
}
