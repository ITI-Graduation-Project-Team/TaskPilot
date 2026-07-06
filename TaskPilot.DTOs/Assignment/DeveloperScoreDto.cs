namespace TaskPilot.DTOs.Assignment;

public class DeveloperScoreDto
{
    public DeveloperSnapshotDto Developer { get; set; } = new();
    public double FinalScore { get; set; }
    public double SkillScore { get; set; }
    public double AvailabilityScore { get; set; }
    public double VelocityScore { get; set; }
    public double ExperienceScore { get; set; }
    public List<SkillGapDto> SkillGaps { get; set; } = new();
}
