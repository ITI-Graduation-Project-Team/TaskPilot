namespace TaskPilot.Application.Common.Errors
{
    /// <summary>
    /// Semantic categories that describe the *nature* of an error,
    /// NOT HTTP status codes.  The API layer maps these to the
    /// appropriate HTTP response on its own.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>General / unspecified failure.</summary>
        Failure = 0,

        /// <summary>One or more input values are invalid.</summary>
        Validation = 1,

        /// <summary>The requested resource does not exist.</summary>
        NotFound = 2,

        /// <summary>A business rule or uniqueness constraint was violated.</summary>
        Conflict = 3,

        /// <summary>The caller is not authenticated.</summary>
        Unauthorized = 4,

        /// <summary>The caller is authenticated but lacks permission.</summary>
        Forbidden = 5
    }
}
