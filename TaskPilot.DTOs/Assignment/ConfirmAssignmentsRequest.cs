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
}

public class TaskAssignmentDto
{
    public Guid TaskId { get; set; }

    /// <summary>
    /// The developer the PM chose — may or may not be the AI top suggestion.
    /// </summary>
    public Guid? EmployeeId { get; set; }
}
