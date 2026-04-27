using System.ComponentModel.DataAnnotations.Schema;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{

    public class UserSkill : BaseEntity<Guid>
    {
        [ForeignKey("Developer")]
        public Guid DeveloperId { get; set; }
        [ForeignKey("Skill")]
        public int SkillId { get; set; }

        public Developer Developer { get; set; } = null!;
        public Skill Skill { get; set; } = null!;

        public int Level { get; set; }
        public int YearsOfExperience { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
