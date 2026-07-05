namespace TaskPilot.Models.Common.Errors;

public static class AssignmentErrors
{
    public static readonly Error ProjectNotFound = new("ASSIGNMENT_PROJECT_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error SprintNotFound = new("ASSIGNMENT_SPRINT_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error SprintDoesNotBelongToProject = new("SPRINT_DOES_NOT_BELONG_TO_PROJECT", ErrorType.Validation);
    public static readonly Error SprintCancelled = new("SPRINT_CANCELLED", ErrorType.Validation);
    public static readonly Error NoProjectTeam = new("NO_PROJECT_TEAM", ErrorType.Validation);
    public static readonly Error HoursExceeded = new("HOURS_EXCEEDED", ErrorType.Validation);
    public static readonly Error NoUnassignedTasks = new("NO_UNASSIGNED_TASKS", ErrorType.Validation);
    public static readonly Error SnapshotUnavailable = new("SNAPSHOT_UNAVAILABLE", ErrorType.Validation);
}
