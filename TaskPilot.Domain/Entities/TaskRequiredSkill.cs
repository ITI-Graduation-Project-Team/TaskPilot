using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class TaskRequiredSkill: AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public int SkillId { get; set; }
        public int RequiredLevel { get; set; }
        public TaskItem Task { get; set; } = null!;
        public Skill Skill { get; set; } = null!;
    }
}
