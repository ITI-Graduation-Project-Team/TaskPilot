using TaskPilot.Models.Common;

namespace TaskPilot.Models.Common.Errors
{
    public static class AgileCoachErrors
    {
        public static Error SummaryGenerationFailed(string? description = null)
            => new("SUMMARY_GENERATION_FAILED", ErrorType.Failure, description ?? "Failed to generate AI summary for the task.");
            
        public static Error ChatGenerationFailed(string? description = null)
            => new("CHAT_GENERATION_FAILED", ErrorType.Failure, description ?? "Failed to generate AI chat response.");

        public static Error KnowledgeBaseEmpty(string? description = null)
            => new("KNOWLEDGE_BASE_EMPTY", ErrorType.Validation, description ?? "No relevant context found in the knowledge base for this task.");
    }
}
