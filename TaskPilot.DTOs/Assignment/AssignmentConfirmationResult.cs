using System.Collections.Generic;

namespace TaskPilot.DTOs.Assignment;

public class AssignmentConfirmationResult
{
    public int TotalRequested { get; set; }
    public int AssignmentsConfirmed { get; set; }
    public int OverridesApplied { get; set; }  // tasks that already had an employee
    public int Skipped { get; set; }            // invalid taskId or employeeId
    public List<string> Warnings { get; set; } = new();
}
