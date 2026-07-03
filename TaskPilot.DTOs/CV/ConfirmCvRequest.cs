using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.CV
{
    public class ConfirmCvRequest
    {
        public string? JobTitle { get; set; }
        public SeniorityLevel? SeniorityLevel { get; set; }
        public decimal? TotalYearsOfExperience { get; set; }
        public List<ConfirmedSkillDto> Skills { get; set; } = new();
    }
}
