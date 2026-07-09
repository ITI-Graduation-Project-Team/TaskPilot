using System.Collections.Generic;

namespace TaskPilot.DTOs.Skills;

public class SkillMergeRequestDto
{
    public int CanonicalSkillId { get; set; }
    public List<int> ObsoleteSkillIds { get; set; } = new();
}
