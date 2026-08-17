using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Assignment;

public class ConfirmAssignmentsRequest
{
    /// <summary>
    /// List of task-to-developer assignments to persist.
    /// PM may include all tasks or only a subset (partial confirm).
    /// Tasks not included remain unchanged (EmployeeId not reset).
    /// </summary>
    public List<TaskAssignmentDto> Assignments { get; set; } = new();
    public bool AllowOverCapacity { get; set; }
}

public class TaskAssignmentDto
{
    public Guid TaskId { get; set; }

    /// <summary>
    /// The developer selected by the project manager; may differ from the top scored candidate.
    /// </summary>
    public Guid? EmployeeId { get; set; }
}
