namespace TaskPilot.DTOs.Employees;

public class AffectedSprintDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid SprintId { get; set; }
    public string SprintTitle { get; set; } = string.Empty;
    public int TaskCount { get; set; }
}
