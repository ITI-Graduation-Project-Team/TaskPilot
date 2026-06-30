namespace TaskPilot.Models.Common.Errors
{
    public static class TechStackErrors
    {
        public static readonly Error GenerationFailed =
            new("TECH_STACK_GENERATION_FAILED", ErrorType.Failure, "Failed to suggest a tech stack.");
    }
}
