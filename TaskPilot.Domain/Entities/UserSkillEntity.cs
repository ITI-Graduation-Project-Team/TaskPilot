using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskPilot.Domain.Entities
{
    public class UserSkillEntity
    {
        public Guid UserId { get; private set; }
        //public UserEntity User { get; private set; }

        public Guid SkillId { get; private set; }
        public SkillEntity Skill { get; private set; }

        public int Level { get; private set; }
        public int YearsOfExperience { get; private set; }

        public DateTime AddedAt { get; private set; }

        private UserSkillEntity() { }

        public UserSkillEntity(Guid userId, Guid skillId, int level, int yearsOfExperience)
        {
            UserId = userId;
            SkillId = skillId;

            Level = level;
            YearsOfExperience = yearsOfExperience;
            AddedAt = DateTime.UtcNow;
        }
    }
}
