namespace TaskPilot.Models.Common.Errors
{
    public static class RequirementErrors
    {
        public static readonly Error SessionAlreadyFinalized =
            new("SESSION_ALREADY_FINALIZED", ErrorType.Conflict, "Requirement session is already finalized.");
        
        public static readonly Error SessionNotPlanning =
            new("SESSION_NOT_PLANNING", ErrorType.Validation, "Session must be in Planning status before finalization.");
        
        public static readonly Error IngestionFailed =
            new("DOCUMENT_INGESTION_FAILED", ErrorType.Failure, "Failed to ingest document.");
    }
}
