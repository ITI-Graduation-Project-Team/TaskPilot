using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{

    public class UserSkill : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }
        public int SkillId { get; set; }

        public User User { get; set; } = null!;
        public Skill Skill { get; set; } = null!;

        public int Level { get; set; }
        public int YearsOfExperience { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
