namespace TaskPilot.Domain.Entities
{
    public class TaskRequiredSkill
    {
        public Guid TaskId { get; set; }
        public int SkillId { get; set; }

        public Task Task { get; set; } = null!;
        public Skill Skill { get; set; } = null!;
    }
}
