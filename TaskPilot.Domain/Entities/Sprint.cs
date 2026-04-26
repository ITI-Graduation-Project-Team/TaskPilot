using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Sprint : AuditableEntity<Guid>
    {
        public Guid ProjectId { get; private set; }
        public Project? Project { get; private set; }

        public string TitleEn { get; private set; }
        public string TitleAr { get; private set; }
        public string? SprintGoalEn { get; private set; }
        public string? SprintGoalAr { get; private set; }

        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        public SprintStatus Status { get; private set; }

        public ICollection<UserStory> UserStories { get; private set; } = new List<UserStory>();
        public ICollection<Task> Tasks { get; private set; } = new List<Task>();

        // EF
        private Sprint() { }

        public Sprint(
            Guid projectId,
            string titleEn,
            string titleAr,
            DateTime startDate,
            DateTime endDate,
            string? sprintGoalEn = null,
            string? sprintGoalAr = null)
        {
            Id = Guid.NewGuid();

            ProjectId = projectId;
            TitleEn = titleEn;
            TitleAr = titleAr;
            SprintGoalEn = sprintGoalEn;
            SprintGoalAr = sprintGoalAr;

            StartDate = startDate;
            EndDate = endDate;

            Status = SprintStatus.Planned;

            //SetCreated();
        }
    }
}
