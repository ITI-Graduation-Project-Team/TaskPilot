using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{

    public class UserSkill : AuditableEntity<Guid>
    {
        public Guid UserId { get; set; }
        public int SkillId { get; set; }

        public User User { get; set; } = null!;
        public Skill Skill { get; set; } = null!;

        public SkillLevel Level { get; set; } = SkillLevel.Intermediate;
        public int? YearsOfExperience { get; set; }
    }
}
