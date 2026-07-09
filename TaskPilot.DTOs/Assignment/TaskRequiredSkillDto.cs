using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Assignment;

public class TaskRequiredSkillDto
{
    public int SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public SkillLevel RequiredLevel { get; set; }
    public List<string> Aliases { get; set; } = new();
}
