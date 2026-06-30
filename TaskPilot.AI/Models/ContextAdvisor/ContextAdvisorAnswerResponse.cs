namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ContextAdvisorAnswerResponse
    {
        public Guid ConversationId { get; set; }

        public string Answer { get; set; } = string.Empty;

        public List<ContextCitation> Citations { get; set; } = new();

        public List<string> SuggestedFollowUps { get; set; } = new();
    }
}
