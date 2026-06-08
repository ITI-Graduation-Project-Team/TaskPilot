namespace TaskPilot.Models.Common.Errors
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

        public static Error Unauthorized(string? description = null)
            => new("UNAUTHORIZED", ErrorType.Unauthorized, description);

        public static Error Forbidden(string? description = null)
            => new("FORBIDDEN", ErrorType.Forbidden, description);

        public static Error InvalidCredentials(string? description = null)
            => new("INVALID_CREDENTIALS", ErrorType.Unauthorized, description);

        public static Error EmailNotConfirmed(string? description = null)
            => new("EMAIL_NOT_CONFIRMED", ErrorType.Unauthorized, description);

        public static Error InvalidRefreshToken(string? description = null)
            => new("INVALID_REFRESH_TOKEN", ErrorType.Unauthorized, description);

        // ──────────────────────── Validation ────────────────────────

        public static Error InvalidInput(string? description = null)
            => new("INVALID_INPUT", ErrorType.Validation, description);

        // ──────────────────────── Resource Lookup ────────────────────────

        // تم دمج resource مع إمكانية تمرير description
        public static Error NotFound(string resource = "Resource", string? description = null)
            => new("NOT_FOUND", ErrorType.NotFound, description ?? $"{resource} was not found.");

        // ──────────────────────── Persistence ────────────────────────

        public static Error SaveFailed(string? description = null)
            => new("SAVE_FAILED", ErrorType.Failure, description);

        public static Error UpdateFailed(string? description = null)
            => new("UPDATE_FAILED", ErrorType.Failure, description);

        public static Error DeleteFailed(string? description = null)
            => new("DELETE_FAILED", ErrorType.Failure, description);

        public static Error RetrieveFailed(string? description = null)
            => new("RETRIEVE_FAILED", ErrorType.Failure, description);

        // ──────────────────────── Business Operations ────────────────────────

        public static Error OperationFailed(string? description = null)
            => new("OPERATION_FAILED", ErrorType.Failure, description);

        public static Error Conflict(string code = "CONFLICT", string? description = null)
            => new(code, ErrorType.Conflict, description);

        public static Error SendCodeFailed(string? description = null)
            => new("SEND_CODE_FAILED", ErrorType.Failure, description);

        // ──────────────────────── Server ────────────────────────

        public static Error ServerError(string? description = null)
            => new("SERVER_ERROR", ErrorType.Failure, description);
    }
}