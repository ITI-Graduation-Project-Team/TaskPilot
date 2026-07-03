namespace TaskPilot.Models.Common.Errors
{
    public static class CvErrors
    {
        public static readonly Error ExtractionFailed = new("CV_EXTRACTION_FAILED", ErrorType.Failure);
        public static readonly Error InvalidFile = new("INVALID_CV_FILE", ErrorType.Validation);
        public static readonly Error FileTooLarge = new("CV_FILE_TOO_LARGE", ErrorType.Validation);
        public static readonly Error UnsupportedFormat = new("CV_UNSUPPORTED_FORMAT", ErrorType.Validation);
        public static readonly Error CvConfirmationRequired = new("CV_CONFIRMATION_REQUIRED", ErrorType.Validation);
        public static readonly Error NoSkillsSelected = new("NO_SKILLS_SELECTED", ErrorType.Validation);
        public static readonly Error DuplicateSkills = new("DUPLICATE_SKILLS", ErrorType.Validation);
        public static readonly Error InvalidSkillName = new("INVALID_SKILL_NAME", ErrorType.Validation);
        public static readonly Error NegativeExperience = new("NEGATIVE_EXPERIENCE", ErrorType.Validation);
        public static readonly Error NullJobTitle = new("NULL_JOB_TITLE", ErrorType.Validation);
        public static readonly Error PrimarySkillRequired = new("PRIMARY_SKILL_REQUIRED", ErrorType.Validation);
        public static readonly Error MultiplePrimarySkills = new("MULTIPLE_PRIMARY_SKILLS", ErrorType.Validation);
    }
}
