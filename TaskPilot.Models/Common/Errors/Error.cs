namespace TaskPilot.Models.Common.Errors
{
    /// <summary>
    /// Represents a domain/application-level error.
    /// Immutable value object — two errors are equal when their Code matches.
    /// </summary>
    public sealed record Error
    {
        /// <summary>
        /// A machine-readable, unique error code (e.g. "RESOURCE_NOT_FOUND").
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A human-readable description of what went wrong.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// The semantic category of this error (Validation, NotFound, Conflict, etc.).
        /// Used by the API layer to decide the appropriate HTTP status code.
        /// </summary>
        public ErrorType Type { get; }

        public Error(string code, ErrorType type = ErrorType.Failure, string? description=null)
        {
            Code = code;
            Description = description;
            Type = type;
        }

        /// <summary>
        /// Represents the absence of an error (used on the success path).
        /// </summary>
        public static readonly Error None = new(string.Empty);
    }
}
