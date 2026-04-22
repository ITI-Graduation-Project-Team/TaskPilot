using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class SkillEntity : BaseEntity<Guid>
    {
        public string Name { get; private set; }

        public ICollection<UserSkillEntity> UserSkills { get; private set; } = new List<UserSkillEntity>();

        private SkillEntity() { }

        public SkillEntity(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
        }
    }
}
