namespace TaskPilot.Models.Common.Errors
{
    public static class ProjectErrors
    {
        public static readonly Error NotFound = new("PROJECT_NOT_FOUND", ErrorType.NotFound);

        public static readonly Error DuplicateAssignment = 
            new("DUPLICATE_ASSIGNMENT", ErrorType.Validation, "Duplicate assignments are forbidden.");

        public static readonly Error EmployeeNotFound = 
            new("EMPLOYEE_NOT_FOUND", ErrorType.Validation, "One or more employees not found.");

        public static readonly Error InvalidCompany = 
            new("INVALID_COMPANY", ErrorType.Validation, "Only Employees from the same Company may be assigned.");

        public static readonly Error AlreadyAssigned = 
            new("ALREADY_ASSIGNED", ErrorType.Validation, "One or more employees are already assigned to the project.");

        public static readonly Error AssignmentNotFound = 
            new("ASSIGNMENT_NOT_FOUND", ErrorType.NotFound, "Employee is not assigned to this project.");

        public static readonly Error EmployeeHasActiveTasks = 
            new("EMPLOYEE_HAS_ACTIVE_TASKS", ErrorType.Validation, "Employee still owns active tasks.");
    }
}
