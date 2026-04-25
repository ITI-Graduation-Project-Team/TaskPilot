using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Sprint : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; private set; }

        public string Title { get; private set; }
        public string? SprintGoal { get; private set; }

        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public SprintStatus Status { get; private set; }

        // EF
        private Sprint() { }

        public Sprint(
            Guid projectId,
            string title,
            DateTime startDate,
            DateTime endDate,
            string? sprintGoal = null)
        {
            Id = Guid.NewGuid();

            ProjectId = projectId;
            Title = title;
            SprintGoal = sprintGoal;

            StartDate = startDate;
            EndDate = endDate;

            Status = SprintStatus.Planned;

            //SetCreated();
        }
    }
}
