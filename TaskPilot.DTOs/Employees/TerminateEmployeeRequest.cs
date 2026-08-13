namespace TaskPilot.DTOs.Employees;
public class TerminateEmployeeRequest { 
    public string? Reason { get; set; } 
    public string? SprintAction { get; set; } // "cancelAndReplan", "cancelOnly", "ignore"
}
