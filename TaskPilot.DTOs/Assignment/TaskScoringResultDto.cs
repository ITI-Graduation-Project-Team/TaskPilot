namespace TaskPilot.DTOs.Assignment;

public class TaskScoringResultDto
{
    public TaskSnapshotDto Task { get; set; } = new();
    public bool IsUnassignable { get; set; }
    public List<DeveloperScoreDto> RankedDevelopers { get; set; } = new();
}
