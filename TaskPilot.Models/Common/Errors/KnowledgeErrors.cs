namespace TaskPilot.Models.Common.Errors
{
    public static class KnowledgeErrors
    {
        public static readonly Error MissingTenantIsolation =
            new("MISSING_TENANT_ISOLATION", ErrorType.Validation, "Either RequirementSessionId, ProjectId, or CompanyId must be provided to ensure multi-tenant isolation.");
    }
}
