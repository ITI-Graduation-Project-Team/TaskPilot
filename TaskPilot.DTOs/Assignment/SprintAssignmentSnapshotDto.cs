namespace TaskPilot.DTOs.Assignment;

using TaskPilot.Models.Enums;

public class SprintAssignmentSnapshotDto
{
    public SprintStatus SprintStatus { get; set; }
    public TeamSnapshotDto Team { get; set; } = new();
    public List<TaskSnapshotDto> UnassignedTasks { get; set; } = new();
}
