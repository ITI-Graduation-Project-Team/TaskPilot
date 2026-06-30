namespace TaskPilot.Models.Common.Errors
{
    public static class UserSubscriptionErrors
    {
        public static readonly Error NotFound = new("USER_SUBSCRIPTION_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error ActiveSubscriptionNotFound = new("ACTIVE_SUBSCRIPTION_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error InvalidBillingCycle = new("INVALID_BILLING_CYCLE", ErrorType.Validation);
        public static readonly Error InvalidStatus = new("INVALID_SUBSCRIPTION_STATUS", ErrorType.Validation);
    }
}
