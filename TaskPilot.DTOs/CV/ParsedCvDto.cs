using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.CV
{
    public class ParsedCvDto
    {
        public string? JobTitle { get; set; }

        public SeniorityLevel? SeniorityLevel { get; set; }

        public int? TotalYearsOfExperience { get; set; }

        public List<ParsedSkillDto> Skills { get; set; } = [];
    }
}
