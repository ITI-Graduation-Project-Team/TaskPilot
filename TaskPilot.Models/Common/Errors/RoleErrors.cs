namespace TaskPilot.Models.Common.Errors
{
    public static class RoleErrors
    {
        public static readonly Error NotFound = new("ROLE_NOT_FOUND", ErrorType.NotFound);
    }
}
