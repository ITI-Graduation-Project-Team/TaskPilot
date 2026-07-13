using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class TaskItem : AuditableEntity<Guid>
    {
        /// <summary>
        /// Null when the TaskItem has been generated but not yet assigned to
        /// a Sprint. Automatically populated with the same SprintId as its
        /// parent UserStory when Sprint assignment is approved (Sprint 5e).
        /// </summary>
        public Guid? SprintId { get; set; }
        public Guid? UserStoryId { get; set; }
        public Guid? EmployeeId { get; set; }
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
        public decimal EstimatedHours { get; set; }
        public decimal ActualHours { get; set; } = 0;
        public EffortSize EffortSize { get; set; }
        public TaskType Type { get; set; }
        public Employee? Employee { get; set; }
        public Sprint? Sprint { get; set; }
        public UserStory? UserStory{ get; set;}
        public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
        public ICollection<TaskRequiredSkill> RequiredSkills { get; set; } = new List<TaskRequiredSkill>();
        public TaskAiSummary? AiSummary { get; set; }
    }
}