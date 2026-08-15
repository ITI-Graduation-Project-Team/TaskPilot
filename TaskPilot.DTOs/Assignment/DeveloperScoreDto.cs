namespace TaskPilot.DTOs.Assignment;

public class DeveloperScoreDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public double FinalScore { get; set; }
    public double SkillScore { get; set; }
    public double AvailabilityScore { get; set; }
    public double VelocityScore { get; set; }
    public double ExperienceScore { get; set; }
    public List<SkillGapDto> SkillGaps { get; set; } = new();
    public double RemainingHours { get; set; }
    public double MaxSprintHours { get; set; }
    public double CurrentAssignedHours { get; set; }
    public bool HasSufficientCapacity { get; set; }
}
