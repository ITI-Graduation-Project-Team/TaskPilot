namespace TaskPilot.Models.Common.Errors
{
    public static class SubscriptionPlanErrors
    {
        public static readonly Error NotFound = new("PLAN_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error NameAlreadyExists = new("PLAN_NAME_ALREADY_EXISTS", ErrorType.Conflict);
    }
}
