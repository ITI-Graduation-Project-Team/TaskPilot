using TaskPilot.AI.Models.Session;

namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ContextAdvisorConversation
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? ProjectId { get; set; }

        public Guid? TaskId { get; set; }

        public List<ConversationMessage> Messages { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
