using Microsoft.AspNetCore.SignalR;
using TaskPilot.DTOs.Projects;
using TaskPilot.Presentation.Hubs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Services
{
    public sealed class ProjectSetupStatusNotifier(
        IHubContext<NotificationHub> hubContext,
        ILogger<ProjectSetupStatusNotifier> logger)
        : IProjectSetupStatusNotifier
    {
        public Task NotifyAsync(Guid userId, ProjectSetupStatusChangedDto statusChange)
        {
            logger.LogInformation(
                "Sending {EventName} for project {ProjectId} to user {UserId} from backend {BackendInstance}",
                "ProjectSetupStatusChanged",
                statusChange.ProjectId,
                userId,
                Environment.MachineName);

            return hubContext.Clients
                .User(userId.ToString())
                .SendAsync("ProjectSetupStatusChanged", statusChange);
        }
    }
}
