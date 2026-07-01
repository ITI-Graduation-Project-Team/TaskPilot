namespace TaskPilot.Models.Common.Errors
{
    public static class SkillErrors
    {
        public static readonly Error NotFound = new("SKILL_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error NameAlreadyExists = new("SKILL_ALREADY_EXISTS", ErrorType.Conflict);
        public static readonly Error InvalidName = new("SKILL_NAME_REQUIRED", ErrorType.Validation);
        public static readonly Error EmptyList = new("SKILL_LIST_EMPTY", ErrorType.Validation);
    }
}
