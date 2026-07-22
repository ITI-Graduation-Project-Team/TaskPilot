using System;
using System.Threading;

namespace TaskPilot.Services.Helpers
{
    public static class AiTelemetryContext
    {
        private static readonly AsyncLocal<Guid?> _currentProjectId = new AsyncLocal<Guid?>();

        public static Guid? CurrentProjectId
        {
            get => _currentProjectId.Value;
            set => _currentProjectId.Value = value;
        }

        public static IDisposable SetProjectId(Guid? projectId)
        {
            var previous = _currentProjectId.Value;
            _currentProjectId.Value = projectId;
            return new ScopeRemover(() => _currentProjectId.Value = previous);
        }

        private class ScopeRemover : IDisposable
        {
            private readonly Action _onDispose;
            public ScopeRemover(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }
}
