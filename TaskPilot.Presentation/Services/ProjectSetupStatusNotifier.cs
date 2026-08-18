using Microsoft.AspNetCore.SignalR;
using TaskPilot.DTOs.Projects;
using TaskPilot.Presentation.Hubs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Services
{
    public sealed class ProjectSetupStatusNotifier(IHubContext<NotificationHub> hubContext)
        : IProjectSetupStatusNotifier
    {
        public Task NotifyAsync(Guid userId, ProjectSetupStatusChangedDto statusChange) =>
            hubContext.Clients
                .User(userId.ToString())
                .SendAsync("ProjectSetupStatusChanged", statusChange);
    }
}
