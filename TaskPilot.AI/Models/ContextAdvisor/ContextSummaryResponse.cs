namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ContextSummaryResponse
    {
        public Guid ConversationId { get; set; }

        public string Summary { get; set; } = string.Empty;

        public List<string> CodebaseNotes { get; set; } = new();

        public List<string> RelatedPastTasks { get; set; } = new();

        public List<string> TechStackContext { get; set; } = new();

        public List<string> SuggestedImplementationGuidance { get; set; } = new();

        public List<ContextCitation> Citations { get; set; } = new();
    }
}
