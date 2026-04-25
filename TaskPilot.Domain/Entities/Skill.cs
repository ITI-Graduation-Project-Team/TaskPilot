using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class Skill : AuditableEntity<int>
    {
        public string Name { get; set; } = string.Empty;

        public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
        public ICollection<TaskRequiredSkill> TaskRequiredSkills { get; set; } = new List<TaskRequiredSkill>();
    }
}
