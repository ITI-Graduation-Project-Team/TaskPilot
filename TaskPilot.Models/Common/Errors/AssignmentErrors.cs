namespace TaskPilot.Models.Common.Errors;

public static class AssignmentErrors
{
    public static readonly Error ProjectNotFound = new("ASSIGNMENT_PROJECT_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error SprintNotFound = new("ASSIGNMENT_SPRINT_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error SprintDoesNotBelongToProject = new("SPRINT_DOES_NOT_BELONG_TO_PROJECT", ErrorType.Validation);
    public static readonly Error SprintCancelled = new("SPRINT_CANCELLED", ErrorType.Validation);
    public static readonly Error NoProjectTeam = new("NO_PROJECT_TEAM", ErrorType.Validation);
    public static readonly Error HoursExceeded = new("HOURS_EXCEEDED", ErrorType.Validation);
    public static readonly Error NoUnassignedTasks = new("NO_UNASSIGNED_TASKS", ErrorType.Validation);
    public static readonly Error SnapshotUnavailable = new("SNAPSHOT_UNAVAILABLE", ErrorType.Validation);

    // Scoring Engine Errors
    public static readonly Error InvalidProject = new("INVALID_PROJECT", ErrorType.NotFound);
    public static readonly Error InvalidSprint = new("INVALID_SPRINT", ErrorType.NotFound);
    public static readonly Error SnapshotNotFound = new("SNAPSHOT_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error CapacityValidationFailed = new("CAPACITY_VALIDATION_FAILED", ErrorType.Validation);
    public static readonly Error ScoringConfigurationInvalid = new("SCORING_CONFIGURATION_INVALID", ErrorType.Failure);

    // Sprint 8c Errors
    public static readonly Error RequiredSkillsNotFound = new("REQUIRED_SKILLS_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error InvalidAvailabilityState = new("INVALID_AVAILABILITY_STATE", ErrorType.Validation);
    public static readonly Error InvalidDeveloperSnapshot = new("INVALID_DEVELOPER_SNAPSHOT", ErrorType.Validation);
    public static readonly Error DuplicateDeveloperDetected = new("DUPLICATE_DEVELOPER_DETECTED", ErrorType.Conflict);

    // Explanation Errors
    public static readonly Error ScoreNotFound = new("SCORE_NOT_FOUND", ErrorType.NotFound);
    public static readonly Error InvalidExplanationInput = new("INVALID_EXPLANATION_INPUT", ErrorType.Validation);
    public static readonly Error InvalidDeveloperContext = new("INVALID_DEVELOPER_CONTEXT", ErrorType.Validation);
    public static readonly Error ExplanationParsingFailed = new("EXPLANATION_PARSING_FAILED", ErrorType.Failure);
    public static readonly Error ExplanationGenerationFailed = new("EXPLANATION_GENERATION_FAILED", ErrorType.Failure);
}
