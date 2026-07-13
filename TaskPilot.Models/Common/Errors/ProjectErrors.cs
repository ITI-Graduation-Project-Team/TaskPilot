namespace TaskPilot.Models.Common.Errors
{
    public static class ProjectErrors
    {
        public static readonly Error NotFound = new("PROJECT_NOT_FOUND", ErrorType.NotFound, "Project.NotFound");
        public static readonly Error InvalidProjectId = new("INVALID_PROJECT_ID", ErrorType.Validation, "Project.InvalidId");
        public static readonly Error InvalidProjectStatus = new("INVALID_PROJECT_STATUS", ErrorType.Validation, "Project.InvalidStatus");
        public static readonly Error ProjectAlreadyCompleted = new("PROJECT_ALREADY_COMPLETED", ErrorType.Conflict, "Project.AlreadyCompleted");
        public static readonly Error ProjectAlreadyArchived = new("PROJECT_ALREADY_ARCHIVED", ErrorType.Conflict, "Project.AlreadyArchived");
        public static readonly Error InvalidStatusTransition = new("INVALID_STATUS_TRANSITION", ErrorType.Validation, "Project.InvalidStatusTransition");
    }
}
