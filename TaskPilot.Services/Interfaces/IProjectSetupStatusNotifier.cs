using TaskPilot.DTOs.Projects;

namespace TaskPilot.Services.Interfaces
{
    public interface IProjectSetupStatusNotifier
    {
        Task NotifyAsync(Guid userId, ProjectSetupStatusChangedDto statusChange);
    }
}
