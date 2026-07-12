namespace TaskPilot.DTOs.Assignment;

public class ExplainedTaskScoringResultDto
{
    public TaskSnapshotDto Task { get; set; } = new();
    public List<ExplainedDeveloperDto> RankedDevelopers { get; set; } = new();
}

public class ExplainedAssignmentDto
{
    public Guid ProjectId { get; set; }
    public Guid SprintId { get; set; }
    public List<ExplainedTaskScoringResultDto> TaskScores { get; set; } = new();
}
