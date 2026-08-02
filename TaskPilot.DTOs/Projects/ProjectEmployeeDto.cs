using System;
using System.Collections.Generic;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Projects;

public class ProjectEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public SeniorityLevel SeniorityLevel { get; set; }
    public int ActiveProjectsCount { get; set; }
    public int CurrentAssignedTasksCount { get; set; }
    public int CurrentSprintHours { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public bool IsDeactivated { get; set; }
    public string? DeactivationReason { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
