using System.ComponentModel.DataAnnotations.Schema;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{

    public class UserSkill : AuditableEntity<Guid>
    {
        public Guid EmployeeId { get; set; }
        public int SkillId { get; set; }

        public Employee Employee { get; set; } = null!;
        public Skill Skill { get; set; } = null!;

        public int Level { get; set; }
        public int YearsOfExperience { get; set; }
    }
}
