namespace TaskPilot.Models.Common.Errors
{
    public static class SkillErrors
    {
        public static readonly Error NotFound = new("SKILL_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error NameAlreadyExists = new("SKILL_NAME_ALREADY_EXISTS", ErrorType.Conflict);
        public static readonly Error InvalidName = new("SKILL_INVALID_NAME", ErrorType.Validation);
        public static readonly Error EmptyList = new("SKILL_EMPTY_LIST", ErrorType.Validation);

        // Migration Errors
        public static readonly Error CanonicalSkillNotFound = new("CANONICAL_SKILL_NOT_FOUND", ErrorType.NotFound);
        public static readonly Error AliasAlreadyExists = new("ALIAS_ALREADY_EXISTS", ErrorType.Conflict);
        public static readonly Error InvalidAlias = new("INVALID_ALIAS", ErrorType.Validation);
        public static readonly Error SkillMigrationFailed = new("SKILL_MIGRATION_FAILED", ErrorType.Failure);
        public static readonly Error DuplicateCanonicalSkill = new("DUPLICATE_CANONICAL_SKILL", ErrorType.Conflict);
        public static readonly Error SkillMergeFailed = new("SKILL_MERGE_FAILED", ErrorType.Failure);
    }
}
