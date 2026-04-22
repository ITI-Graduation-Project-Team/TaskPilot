using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class TaskEntity : AuditableEntity<Guid>
    {
        public Guid SprintId { get; private set; }
        public Guid? UserStoryId { get; private set; }
        public Guid? AssigneeId { get; private set; }

        public string Title { get; private set; }
        public string? Description { get; private set; }
        public string? TechnicalSummary { get; private set; }
        public string? AcceptanceCriteria { get; private set; }

        public TaskPriority Priority { get; private set; }
        public TaskItemStatus Status { get; private set; }

        public float EstimatedHours { get; private set; }
        public float ActualHours { get; private set; }

        private TaskEntity() { }

        public TaskEntity(
            Guid sprintId,
            string title,
            float estimatedHours,
            TaskPriority priority)
        {
            Id = Guid.NewGuid();

            SprintId = sprintId;
            Title = title;
            EstimatedHours = estimatedHours;
            Priority = priority;

            Status = TaskItemStatus.ToDo;
            ActualHours = 0;

            //SetCreated();
        }
    }
}
