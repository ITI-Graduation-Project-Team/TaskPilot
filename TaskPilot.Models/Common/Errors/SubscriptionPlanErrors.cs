namespace TaskPilot.Models.Common.Errors
{
    public static class SubscriptionPlanErrors
    {
        public static readonly Error NotFound = new("SUBSCRIPTION_PLAN_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error NameAlreadyExists = new("SUBSCRIPTION_PLAN_NAME_EXISTS", ErrorType.Conflict);
    }
}
