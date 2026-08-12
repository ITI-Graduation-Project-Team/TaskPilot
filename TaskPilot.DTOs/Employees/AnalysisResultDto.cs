namespace TaskPilot.DTOs.Employees;

public class AnalysisResultDto
{
    public bool IsAllowed { get; set; }
    public List<DeactivationBlock> Blocks { get; set; } = new();
    public bool HasPlannedSprintTasks { get; set; }           
    public List<AffectedSprintDto> AffectedSprints { get; set; } = new(); 
}
