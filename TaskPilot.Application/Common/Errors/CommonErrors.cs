namespace TaskPilot.Application.Common.Errors
{
    /// <summary>
    /// A catalogue of reusable, domain-level error instances.
    /// Covers the most common failure scenarios across the application.
    /// 
    /// Usage:  return Result.Failure(CommonErrors.NotFound("Project"));
    /// </summary>
    public static class CommonErrors
    {
        // ──────────────────────── Authentication / Authorization ────────────────────────

        public static Error Unauthorized(string description = "Authentication is required.")
            => new("UNAUTHORIZED", description, ErrorType.Unauthorized);

        public static Error Forbidden(string description = "You do not have permission to perform this action.")
            => new("FORBIDDEN", description, ErrorType.Forbidden);

        public static Error InvalidCredentials(string description = "The email or password is incorrect.")
            => new("INVALID_CREDENTIALS", description, ErrorType.Unauthorized);

        public static Error EmailNotConfirmed(string description = "The email address has not been confirmed.")
            => new("EMAIL_NOT_CONFIRMED", description, ErrorType.Unauthorized);

        public static Error InvalidRefreshToken(string description = "The refresh token is invalid or expired.")
            => new("INVALID_REFRESH_TOKEN", description, ErrorType.Unauthorized);

        // ──────────────────────── Validation ────────────────────────

        public static Error InvalidInput(string description = "One or more input values are invalid.")
            => new("INVALID_INPUT", description, ErrorType.Validation);

        // ──────────────────────── Resource Lookup ────────────────────────

        public static Error NotFound(string resource = "Resource")
            => new("NOT_FOUND", $"{resource} was not found.", ErrorType.NotFound);

        // ──────────────────────── Persistence ────────────────────────

        public static Error SaveFailed(string description = "Failed to save data.")
            => new("SAVE_FAILED", description, ErrorType.Failure);

        public static Error UpdateFailed(string description = "Failed to update data.")
            => new("UPDATE_FAILED", description, ErrorType.Failure);

        public static Error DeleteFailed(string description = "Failed to delete data.")
            => new("DELETE_FAILED", description, ErrorType.Failure);

        public static Error RetrieveFailed(string description = "Failed to retrieve data.")
            => new("RETRIEVE_FAILED", description, ErrorType.Failure);

        // ──────────────────────── Business Operations ────────────────────────

        public static Error OperationFailed(string description = "The operation could not be completed.")
            => new("OPERATION_FAILED", description, ErrorType.Failure);

        public static Error Conflict(string description = "A conflict occurred with the current state.")
            => new("CONFLICT", description, ErrorType.Conflict);

        public static Error SendCodeFailed(string description = "Failed to send the verification code.")
            => new("SEND_CODE_FAILED", description, ErrorType.Failure);

        // ──────────────────────── Server ────────────────────────

        public static Error ServerError(string description = "An unexpected error occurred on the server.")
            => new("SERVER_ERROR", description, ErrorType.Failure);
    }
}
