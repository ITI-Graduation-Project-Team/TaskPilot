using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using TaskPilot.DTOs.Notifications;
using TaskPilot.Presentation.Hubs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Services
{
    public class NotificationNotifier : INotificationNotifier
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationNotifier> _logger;

        public NotificationNotifier(
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyAsync(Guid userId, NotificationDto notification)
        {
            _logger.LogInformation(
                "Sending {EventName} notification {NotificationId} to user {UserId} from backend {BackendInstance}",
                "ReceiveNotification",
                notification.Id,
                userId,
                Environment.MachineName);

            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync("ReceiveNotification", notification);
        }
    }
}
