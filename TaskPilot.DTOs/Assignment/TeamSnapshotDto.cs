namespace TaskPilot.DTOs.Assignment;

public class TeamSnapshotDto
{
    public Guid ProjectId { get; set; }
    public Guid SprintId { get; set; }
    public int TeamSize { get; set; }
    public double TotalTeamRemainingHours { get; set; }
    public List<DeveloperSnapshotDto> Developers { get; set; } = new();
}
