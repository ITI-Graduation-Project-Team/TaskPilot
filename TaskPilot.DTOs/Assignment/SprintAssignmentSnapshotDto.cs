namespace TaskPilot.DTOs.Assignment;

public class SprintAssignmentSnapshotDto
{
    public TeamSnapshotDto Team { get; set; } = new();
    public List<TaskSnapshotDto> UnassignedTasks { get; set; } = new();
}
