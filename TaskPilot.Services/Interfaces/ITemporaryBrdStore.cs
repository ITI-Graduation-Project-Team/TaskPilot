using System;

namespace TaskPilot.Services.Interfaces
{
    public interface ITemporaryBrdStore
    {
        void Store(Guid projectId, string brdText);
        string? Retrieve(Guid projectId);
    }
}
