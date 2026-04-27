using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Task : AuditableEntity<Guid>
    {
        public Guid SprintId { get; set; }
        public Guid? UserStoryId { get; set; }
        public Guid? DeveloperId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;

        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }

        public string? TechnicalSummaryEn { get; set; }
        public string? TechnicalSummaryAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }

        public TaskPriority Priority { get; set; }
        public TaskItemStatus Status { get; set; } = TaskItemStatus.ToDo;
        public Developer Developer { get; set; }

        public float EstimatedHours { get; set; }
        public float ActualHours { get; set; } = 0;
    }
}