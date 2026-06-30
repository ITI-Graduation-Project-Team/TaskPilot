namespace TaskPilot.Models.Common.Errors
{
    public static class KnowledgeErrors
    {
        public static readonly Error EmptySessionId =
            new("EMPTY_SESSION_ID", ErrorType.Validation, "SessionId cannot be empty.");
    }
}
