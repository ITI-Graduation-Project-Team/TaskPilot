using System.ComponentModel.DataAnnotations.Schema;

namespace TaskPilot.Domain.Entities
{
    public class TaskRequiredSkill
    {
        [ForeignKey("Task")]
        public Guid TaskId { get; set; }
        [ForeignKey("Skill")]
        public int SkillId { get; set; }
        public Task Task { get; set; }
        public Skill Skill { get; set; }
    }
}
