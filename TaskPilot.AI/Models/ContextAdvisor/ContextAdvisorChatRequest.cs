namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ContextAdvisorChatRequest : TaskContextRequest
    {
        public Guid? ConversationId { get; set; }

        public string Question { get; set; } = string.Empty;
    }
}
