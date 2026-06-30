namespace TaskPilot.Models.Common.Errors
{
    public static class EmployeeErrors
    {
        public static readonly Error UserNotEmployee =
            new("USER_NOT_EMPLOYEE", ErrorType.Forbidden, "User is not an employee.");
    }
}
