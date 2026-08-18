using System;
using System.Threading;

namespace TaskPilot.Services.Helpers
{
    public static class AiTelemetryContext
    {
        private static readonly AsyncLocal<Guid?> _currentProjectId = new AsyncLocal<Guid?>();
        private static readonly AsyncLocal<Guid?> _currentUserId = new AsyncLocal<Guid?>();

        public static Guid? CurrentProjectId
        {
            get => _currentProjectId.Value;
            set => _currentProjectId.Value = value;
        }

        public static Guid? CurrentUserId
        {
            get => _currentUserId.Value;
            set => _currentUserId.Value = value;
        }

        public static IDisposable SetProjectId(Guid? projectId)
        {
            var previous = _currentProjectId.Value;
            _currentProjectId.Value = projectId;
            return new ScopeRemover(() => _currentProjectId.Value = previous);
        }

        public static IDisposable SetContext(Guid? userId, Guid? projectId)
        {
            var previousUserId = _currentUserId.Value;
            var previousProjectId = _currentProjectId.Value;
            _currentUserId.Value = userId;
            _currentProjectId.Value = projectId;
            return new ScopeRemover(() =>
            {
                _currentUserId.Value = previousUserId;
                _currentProjectId.Value = previousProjectId;
            });
        }

        private class ScopeRemover : IDisposable
        {
            private readonly Action _onDispose;
            public ScopeRemover(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }
}
