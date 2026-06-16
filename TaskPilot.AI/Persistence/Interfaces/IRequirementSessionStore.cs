using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Session;

namespace TaskPilot.AI.Persistence.Interfaces
{
    public interface IRequirementSessionStore
    {
        Task SaveAsync(
            RequirementSession session,
            CancellationToken cancellationToken = default);

        Task<RequirementSession?>
            GetAsync(
                Guid sessionId,
                CancellationToken cancellationToken = default);
    }
}