namespace TaskPilot.DTOs.Assignment;

public class AssignTaskRequest
{
    public Guid? EmployeeId { get; set; }
    public bool AllowOverCapacity { get; set; }
}

public class AssignTaskResult
{
    public Guid TaskId { get; set; }
    public Guid? PreviousEmployeeId { get; set; }
    public Guid? EmployeeId { get; set; }
    public bool Changed { get; set; }
    public double? AssignedHours { get; set; }
    public double? MaxSprintHours { get; set; }
    public List<string> Warnings { get; set; } = new();
}
