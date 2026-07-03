using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.CV
{
    public class ParsedSkillDto
    {
        public string Name { get; set; } = string.Empty;

        public SkillLevel? Level { get; set; }

        public double? YearsOfExperience { get; set; }

        public double ConfidenceScore { get; set; }

        public bool IsPrimarySuggested { get; set; }
    }
}
