using TaskPilot.AI.Models.ContextAdvisor;

namespace TaskPilot.AI.Persistence.Interfaces
{
    public interface IContextAdvisorConversationStore
    {
        Task<ContextAdvisorConversation?> GetAsync(
            Guid conversationId,
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            ContextAdvisorConversation conversation,
            CancellationToken cancellationToken = default);
    }
}
