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

        public NotificationNotifier(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyAsync(Guid userId, NotificationDto notification)
        {
            await _hubContext.Clients
                .User(userId.ToString())
                .SendAsync("ReceiveNotification", notification);
        }
    }
}
