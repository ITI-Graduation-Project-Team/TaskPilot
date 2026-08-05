using System;
using System.Collections.Generic;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Company;

public class CompanyEmployeeDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string SeniorityLevel { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public int ActiveProjectsCount { get; set; }
    public int CurrentAssignedTasksCount { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty;
    public bool IsDeactivated { get; set; }
    public string? DeactivationReason { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
