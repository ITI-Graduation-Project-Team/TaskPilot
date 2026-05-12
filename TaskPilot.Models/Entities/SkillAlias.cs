using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Entities
{
    public class SkillAlias
    {
        public int Id { get; set; }
        public int SkillId { get; set; }
        public string Alias { get; set; } = string.Empty;
        public Skill Skill { get; set; } = null!;
    }
}
