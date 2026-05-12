using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{

    public class UserSkill : AuditableEntity<Guid>
    {
        public Guid UserId { get; set; }
        public int SkillId { get; set; }
        public SkillLevel Level { get; set; }
        public double? YearsOfExperience { get; set; }
        public bool IsPrimary { get; set; }
        public double ConfidenceScore { get; set; }

        public User User { get; set; } = null!;
        public Skill Skill { get; set; } = null!;
    }
}
