namespace TaskPilot.DTOs.Assignment;

public class ScoredAssignmentDto
{
    public Guid ProjectId { get; set; }
    public Guid SprintId { get; set; }
    public List<TaskScoringResultDto> TaskScores { get; set; } = new();
}
