using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Models.Common.Errors
{
    public static class TaskErrors
    {
        public static readonly Error TaskNotFound = new(
            "TASK_NOT_FOUND",
            ErrorType.NotFound,
            "The requested task was not found.");

        public static readonly Error SprintNotActive = new(
            "TASK_SPRINT_NOT_ACTIVE",
            ErrorType.Validation,
            "Tasks can only be updated when the sprint is active.");

        public static readonly Error ActiveSprintNotFound = new(
            "TASK_ACTIVE_SPRINT_NOT_FOUND",
            ErrorType.NotFound,
            "No active sprint was found for this project.");

        public static readonly Error ForbiddenTaskUpdate = new(
            "TASK_FORBIDDEN_UPDATE",
            ErrorType.Forbidden,
            "You are not authorized to update this task.");

        public static readonly Error ActualHoursRequired = new(
            "TASK_ACTUAL_HOURS_REQUIRED",
            ErrorType.Validation,
            "Actual hours are required when completing a task.");

        public static readonly Error InvalidActualHours = new(
            "TASK_INVALID_ACTUAL_HOURS",
            ErrorType.Validation,
            "Actual hours must be greater than zero.");

        public static readonly Error InvalidTaskStatusTransition = new(
            "TASK_INVALID_STATUS_TRANSITION",
            ErrorType.Validation,
            "The requested task status transition is not allowed.");

        public static readonly Error TaskAlreadyInRequestedStatus = new(
            "TASK_ALREADY_IN_REQUESTED_STATUS",
            ErrorType.Validation,
            "The task is already in the requested status.");
    }
}
