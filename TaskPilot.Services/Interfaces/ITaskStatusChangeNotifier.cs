using TaskPilot.DTOs.Tasks;

namespace TaskPilot.Services.Interfaces
{
    public interface ITaskStatusChangeNotifier
    {
        Task NotifyAsync(Guid userId, TaskStatusChangedDto statusChange);
    }
}
