using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Assignment;

public class DeveloperSnapshotDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public ProjectRole ProjectRole { get; set; }
    public SeniorityLevel SeniorityLevel { get; set; }
    public EmployeeAvailabilityStatus AvailabilityStatus { get; set; }
    public List<DeveloperSkillDto> Skills { get; set; } = new();
    public double MaxSprintHours { get; set; }
    public double CurrentAssignedHours { get; set; }
    public double RemainingHours { get; set; }
    public double WorkloadPercentage { get; set; }
    public double? HistoricalVelocity { get; set; }
    public bool HasHistoricalData { get; set; }
    public int CompletedSprintsCount { get; set; }
    public int ActiveTasksCount { get; set; }
}
