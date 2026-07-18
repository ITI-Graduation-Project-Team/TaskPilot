using System;
using System.Collections.Concurrent;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class InMemoryTemporaryBrdStore : ITemporaryBrdStore
    {
        private readonly ConcurrentDictionary<Guid, string> _store = new ConcurrentDictionary<Guid, string>();

        public void Store(Guid projectId, string brdText)
        {
            _store[projectId] = brdText;
        }

        public string? Retrieve(Guid projectId)
        {
            if (_store.TryGetValue(projectId, out var text))
            {
                return text;
            }
            return null;
        }
    }
}
