namespace TaskPilot.Models.Common.Errors
{
    public static class SprintErrors
    {
        public static readonly Error NoUserStoriesSelected =
            new("NO_USER_STORIES_SELECTED", ErrorType.Validation, "At least one UserStory must be selected for the sprint.");

        public static readonly Error SprintNotFound = new("SPRINT_NOT_FOUND", ErrorType.NotFound, "Sprint.NotFound");
        public static readonly Error ProjectNotFound = new("PROJECT_NOT_FOUND", ErrorType.NotFound, "Project.NotFound");
        public static readonly Error SprintAlreadyActive = new("SPRINT_ALREADY_ACTIVE", ErrorType.Conflict, "Sprint.AlreadyActive");
        public static readonly Error SprintAlreadyCompleted = new("SPRINT_ALREADY_COMPLETED", ErrorType.Conflict, "Sprint.AlreadyCompleted");
        public static readonly Error SprintNotStarted = new("SPRINT_NOT_STARTED", ErrorType.Conflict, "Sprint.NotStarted");
        public static readonly Error AnotherSprintAlreadyActive = new("ANOTHER_SPRINT_ALREADY_ACTIVE", ErrorType.Conflict, "Sprint.AnotherAlreadyActive");
        public static readonly Error SprintDoesNotBelongToProject = new("SPRINT_DOES_NOT_BELONG_TO_PROJECT", ErrorType.Validation, "Sprint.DoesNotBelongToProject");
        public static readonly Error InvalidSprintStatus = new("INVALID_SPRINT_STATUS", ErrorType.Validation, "Sprint.InvalidStatus");
        public static readonly Error InvalidSprint = new("INVALID_SPRINT", ErrorType.Validation, "Sprint.Invalid");
        public static readonly Error InvalidProject = new("INVALID_PROJECT", ErrorType.Validation, "Project.Invalid");
        public static readonly Error NoEmployeesAssigned = new("NO_EMPLOYEES_ASSIGNED", ErrorType.Validation, "Cannot perform sprint planning for a project with no assigned employees.");
        public static readonly Error UnassignedTasksExist = new("SPRINT_UNASSIGNED_TASKS_EXIST", ErrorType.Validation, "Cannot start the sprint. All tasks must be assigned to employees.");
    }
}
