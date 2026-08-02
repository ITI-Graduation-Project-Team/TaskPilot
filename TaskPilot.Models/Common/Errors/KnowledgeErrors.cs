namespace TaskPilot.Models.Common.Errors
{
    public static class KnowledgeErrors
    {
        public static readonly Error MissingTenantIsolation =
            new("MISSING_TENANT_ISOLATION", ErrorType.Validation, "Either RequirementSessionId, ProjectId, or CompanyId must be provided to ensure multi-tenant isolation.");

        public static readonly Error AmbiguousTenantIdentifier =
            new("AMBIGUOUS_TENANT_IDENTIFIER", ErrorType.Validation, "Provide either ProjectId or RequirementSessionId, not both.");

        public static readonly Error MissingProjectPolicyIdentifier =
            new("MISSING_PROJECT_POLICY_IDENTIFIER", ErrorType.Validation, "Either ProjectId or RequirementSessionId must be provided.");
    }
}
