using System.Collections.Concurrent;
using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Persistence.Interfaces;

namespace TaskPilot.AI.Persistence.InMemory
{
    public class InMemoryRequirementSessionStore
        : IRequirementSessionStore
    {
        private static readonly
            ConcurrentDictionary<
                Guid,
                RequirementSession>
                    _sessions = new();

        public Task SaveAsync(
            RequirementSession session,
            CancellationToken cancellationToken = default)
        {
            _sessions[
                session.SessionId]
                    = session;

            return Task.CompletedTask;
        }

        public Task<RequirementSession?>
            GetAsync(
                Guid sessionId,
                CancellationToken cancellationToken = default)
        {
            _sessions.TryGetValue(
                sessionId,
                out var session);

            return Task.FromResult(session);
        }
    }
}