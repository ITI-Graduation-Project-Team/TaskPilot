namespace TaskPilot.Models.Common.Errors
{
    public static class UserErrors
    {
        public static readonly Error NotFound = new("USER_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error ProjectManagerNotFound = new("PROJECT_MANAGER_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error NotProjectManager = new("NOT_PROJECT_MANAGER", ErrorType.Forbidden);
    }
}
