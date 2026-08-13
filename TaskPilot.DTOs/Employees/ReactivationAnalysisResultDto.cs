namespace TaskPilot.DTOs.Employees;
using System.Collections.Generic;

public class ReactivationAnalysisResultDto
{
    public bool HasRestorableProjects { get; set; }
    public List<string> RestorableProjectNames { get; set; } = new();
}
