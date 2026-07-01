namespace TaskPilot.Models.Common.Errors
{
    public static class WbsErrors
    {
        public static readonly Error GenerationFailed =
            new("WBS_GENERATION_FAILED", ErrorType.Failure, "Failed to generate WBS.");

        public static readonly Error BacklogAlreadyExists = 
            new("BACKLOG_ALREADY_EXISTS", ErrorType.Conflict, "This project already contains a generated backlog. Review the existing backlog or use the Regenerate endpoint to replace it.");
    }
}
