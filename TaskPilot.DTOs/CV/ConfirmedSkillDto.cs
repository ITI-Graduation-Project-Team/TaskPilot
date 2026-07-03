using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.CV
{
    public class ConfirmedSkillDto
    {
        public string Name { get; set; } = string.Empty;
        public SkillLevel Level { get; set; }
        public decimal YearsOfExperience { get; set; }
        public bool IsPrimary { get; set; }
    }
}
