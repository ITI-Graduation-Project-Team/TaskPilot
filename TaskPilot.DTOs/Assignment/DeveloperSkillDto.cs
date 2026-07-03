using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Assignment;

public class DeveloperSkillDto
{
    public int SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public SkillLevel Level { get; set; }
    public int YearsOfExperience { get; set; }
    public bool IsPrimary { get; set; }
}
