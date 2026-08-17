namespace TaskPilot.DTOs.Assignment;

public class ScoredAssignmentDto
{
    public Guid ProjectId { get; set; }
    public Guid SprintId { get; set; }
    public ScoringWeightsDto Weights { get; set; } = new();
    public List<TaskScoringResultDto> TaskScores { get; set; } = new();
}

public class ScoringWeightsDto
{
    public int SkillWeight { get; set; }
    public int AvailabilityWeight { get; set; }
    public int VelocityWeight { get; set; }
    public int ExperienceWeight { get; set; }
}
