namespace TaskPilot.Models.Common.Errors
{
    public static class WbsErrors
    {
        public static readonly Error GenerationFailed =
            new("WBS_GENERATION_FAILED", ErrorType.Failure, "Failed to generate WBS.");
    }
}
