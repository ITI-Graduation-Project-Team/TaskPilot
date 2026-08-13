namespace TaskPilot.Models.Enums
{
    public enum NotificationType
    {
        TaskAssigned = 0,
        TaskUpdated = 1,
        TaskCompleted = 2,
        TaskOverdue = 3,

        UserStoryUpdated = 4,
        SprintStarted = 5,
        SprintEnded = 6,

        ProjectCreated = 7,
        ProjectUpdated = 8,

        CommentAdded = 9,

        UserAddedToProject = 10,
        SubscriptionExpiring = 11,
        PaymentSuccess = 12,
        PaymentFailed = 13,
        BugReported = 14,
        SprintRiskDetected = 15,
        EmployeeDeactivated = 16,
        BacklogGenerated = 17,
        ProjectSetupCompleted = 18,
        ProjectSetupFailed = 19
    }
}
