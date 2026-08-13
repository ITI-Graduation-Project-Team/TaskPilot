namespace TaskPilot.Models.Enums
{
    public enum TechStackSetupStatus
    {
        NotStarted = 0,
        Suggested = 1,
        Confirmed = 2,
        Failed = 3
    }

    public enum BackgroundSetupStatus
    {
        NotStarted = 0,
        Queued = 1,
        Running = 2,
        Succeeded = 3,
        PartiallySucceeded = 4,
        Failed = 5
    }

    public enum ProjectSetupOverallStatus
    {
        NeedsTechStack = 0,
        ReadyForWbs = 1,
        WbsQueued = 2,
        WbsGenerating = 3,
        WbsReady = 4,
        EnrichingSkills = 5,
        Ready = 6,
        ReadyWithWarnings = 7,
        Failed = 8
    }
}
