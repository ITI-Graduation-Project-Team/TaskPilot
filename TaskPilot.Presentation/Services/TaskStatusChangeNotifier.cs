using Microsoft.AspNetCore.SignalR;
using TaskPilot.DTOs.Tasks;
using TaskPilot.Presentation.Hubs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Services
{
    public sealed class TaskStatusChangeNotifier(
        IHubContext<NotificationHub> hubContext,
        ILogger<TaskStatusChangeNotifier> logger)
        : ITaskStatusChangeNotifier
    {
        public Task NotifyAsync(Guid userId, TaskStatusChangedDto statusChange)
        {
            logger.LogInformation(
                "Sending {EventName} for task {TaskId} in sprint {SprintId} to user {UserId} from backend {BackendInstance}",
                "TaskStatusChanged",
                statusChange.TaskId,
                statusChange.SprintId,
                userId,
                Environment.MachineName);

            return hubContext.Clients
                .User(userId.ToString())
                .SendAsync("TaskStatusChanged", statusChange);
        }
    }
}
