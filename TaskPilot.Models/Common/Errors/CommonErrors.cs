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

        // ──────────────────────── Entitlements / Subscriptions ────────────────────────

        public static Error NoActiveSubscription(string? description = null)
            => new("NO_ACTIVE_SUBSCRIPTION", ErrorType.Forbidden, description ?? "You do not have an active subscription plan.");

        public static Error MaxProjectsLimitReached(int limit, int current) =>
            new Error(
                "MAX_PROJECTS_REACHED",
                ErrorType.Forbidden,
                $"Your plan limits you to {limit} active project(s). You currently have {current}.",
                metadata: new Dictionary<string, object>
                {
                    { "Limit", limit },
                    { "CurrentCount", current }
                });
                
        public static Error StorageLimitReached(double limitMb, double currentMb) =>
            new Error(
                "STORAGE_LIMIT_REACHED",
                ErrorType.Forbidden,
                $"Your plan limits you to {limitMb:F2} MB of storage. You currently have used {currentMb:F2} MB.",
                metadata: new Dictionary<string, object>
                {
                    { "LimitMb", limitMb },
                    { "CurrentMb", currentMb }
                });

        public static Error MaxTeamMembersLimitReached(int limit, int currentCount)
            => new("MAX_TEAM_MEMBERS_REACHED", ErrorType.Forbidden, $"Maximum number of team members per project ({limit}) has been reached. Please upgrade your subscription.",
                new Dictionary<string, object>
                {
                    { "Limit", limit },
                    { "CurrentCount", currentCount }
                });
    }
}