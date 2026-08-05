using System;
using System.Collections.Generic;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Projects;

public class AssignProjectEmployeesRequest
{
    public List<ProjectEmployeeAssignmentDto> Assignments { get; set; } = new();
}

public class ProjectEmployeeAssignmentDto
{
    public Guid EmployeeId { get; set; }
    public ProjectRole Role { get; set; }
    public decimal AllocationPercentage { get; set; } = 100m;
}
