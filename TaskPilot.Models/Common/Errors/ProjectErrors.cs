namespace TaskPilot.Models.Common.Errors
{
    public static class ProjectErrors
    {
        public static readonly Error NotFound = new("PROJECT_NOT_FOUND", ErrorType.NotFound);
    }
}
