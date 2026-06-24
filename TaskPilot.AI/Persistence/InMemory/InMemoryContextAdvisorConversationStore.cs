using System.Collections.Concurrent;
using TaskPilot.AI.Models.ContextAdvisor;
using TaskPilot.AI.Persistence.Interfaces;

namespace TaskPilot.AI.Persistence.InMemory
{
    public class InMemoryContextAdvisorConversationStore
        : IContextAdvisorConversationStore
    {
        private static readonly ConcurrentDictionary<Guid, ContextAdvisorConversation> _conversations = new();

        public Task<ContextAdvisorConversation?> GetAsync(
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            _conversations.TryGetValue(conversationId, out var conversation);

            return Task.FromResult(conversation);
        }

        public Task SaveAsync(
            ContextAdvisorConversation conversation,
            CancellationToken cancellationToken = default)
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            _conversations[conversation.Id] = conversation;

            return Task.CompletedTask;
        }
    }
}
