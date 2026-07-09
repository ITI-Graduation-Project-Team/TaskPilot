namespace TaskPilot.Models.Common.Errors
{
    public static class WbsErrors
    {
        public static readonly Error GenerationFailed =
            new("WBS_GENERATION_FAILED", ErrorType.Failure, "Failed to generate WBS.");

        public static readonly Error RequiredSkillsGenerationFailed = new("REQUIRED_SKILLS_GENERATION_FAILED", ErrorType.Failure);
        public static readonly Error RequiredSkillsPersistenceFailed = new("REQUIRED_SKILLS_PERSISTENCE_FAILED", ErrorType.Failure);
        public static readonly Error RequiredSkillNotFound = new("REQUIRED_SKILL_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error InvalidRequiredSkill = new("INVALID_REQUIRED_SKILL", ErrorType.Validation);
        public static readonly Error AvailableSkillsNotLoaded = new("AVAILABLE_SKILLS_NOT_LOADED", ErrorType.Failure);
        public static readonly Error InvalidGeneratedTask = new("INVALID_GENERATED_TASK", ErrorType.Validation);
        public static readonly Error InvalidGeneratedUserStory = new("INVALID_GENERATED_USER_STORY", ErrorType.Validation);
        public static readonly Error GenerationTruncated = new("GENERATION_TRUNCATED", ErrorType.Failure);
        public static readonly Error ResponseTooLarge = new("RESPONSE_TOO_LARGE", ErrorType.Failure);
        public static readonly Error InvalidGeneratedJson = new("INVALID_GENERATED_JSON", ErrorType.Failure);

        public static readonly Error ProjectNotFound = new("PROJECT_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error NoTasksToEnrich = new("NO_TASKS_TO_ENRICH", ErrorType.Validation);
        public static readonly Error RequiredSkillsEmpty = new("REQUIRED_SKILLS_EMPTY", ErrorType.Validation);
        public static readonly Error SkillNormalizationFailed = new("SKILL_NORMALIZATION_FAILED", ErrorType.Failure);
        public static readonly Error SkillCreationFailed = new("SKILL_CREATION_FAILED", ErrorType.Failure);
        public static readonly Error InvalidGeneratedSkill = new("INVALID_GENERATED_SKILL", ErrorType.Validation);
        public static readonly Error InvalidGeneratedSkillJson = new("INVALID_GENERATED_SKILL_JSON", ErrorType.Failure);
        public static readonly Error InvalidRequiredLevel = new("INVALID_REQUIRED_LEVEL", ErrorType.Validation);
    }
}
