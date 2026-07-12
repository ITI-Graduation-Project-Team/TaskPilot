using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public sealed class SprintLifecycleService(
        IRepository<Sprint> sprintRepository,
        IUnitOfWork unitOfWork) : ISprintLifecycleService
    {
        public async Task<bool> EnsureCompletedIfDueAsync(Guid sprintId, CancellationToken cancellationToken = default)
        {
            var sprint = await sprintRepository.GetByIdAsync(sprintId);

            if (sprint is null || sprint.IsDeleted || sprint.Status == SprintStatus.Cancelled)
                return false;

            if (sprint.Status == SprintStatus.Completed)
                return true;

            // A previously scheduled job must do nothing if the end date was extended.
            if (sprint.EndDate > DateTime.UtcNow)
                return false;

            sprint.Status = SprintStatus.Completed;
            sprintRepository.Update(sprint);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}