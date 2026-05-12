using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class TaskRequiredSkill: AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public int SkillId { get; set; }
        public SkillLevel RequiredLevel { get; set; }
        public TaskItem Task { get; set; } = null!;
        public Skill Skill { get; set; } = null!;
    }
}
