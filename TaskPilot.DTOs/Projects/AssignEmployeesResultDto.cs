namespace TaskPilot.DTOs.Projects;
using System.Collections.Generic;

public class AssignEmployeesResultDto
{
    public bool HasPlannedSprints { get; set; }
    public List<string> PlannedSprintNames { get; set; } = new();
    public List<Guid> PlannedSprintIds { get; set; } = new();
    public List<Guid> SprintProjectIds { get; set; } = new();
}
