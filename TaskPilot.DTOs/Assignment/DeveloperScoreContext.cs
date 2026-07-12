namespace TaskPilot.DTOs.Assignment;

public class DeveloperScoreContext
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public double FinalScore { get; set; }
    public double SkillScore { get; set; }
    public double AvailabilityScore { get; set; }
    public double VelocityScore { get; set; }
    public double ExperienceScore { get; set; }
    public double RemainingHours { get; set; }
    public bool HasSufficientCapacity { get; set; }
    public List<SkillGapDto> SkillGaps { get; set; } = new();
}

public class ExplanationContextDto
{
    public string TaskTitle { get; set; } = string.Empty;
    public decimal TaskEstimatedHours { get; set; }
    public List<TaskRequiredSkillDto> RequiredSkills { get; set; } = new();
    public List<DeveloperScoreContext> TopDevelopers { get; set; } = new();
}
