using System;
using System.Threading.Tasks;
using TaskPilot.DTOs.Notifications;

namespace TaskPilot.Services.Interfaces
{
    public interface INotificationNotifier
    {
        Task NotifyAsync(Guid userId, NotificationDto notification);
    }
}
