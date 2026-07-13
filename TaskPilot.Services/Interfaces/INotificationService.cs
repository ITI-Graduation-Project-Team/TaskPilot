using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskPilot.DTOs.Notifications;
using TaskPilot.Models.Enums;

using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface INotificationService
    {
        Task<Result> SendAsync(Guid userId, NotificationType type, string messageEn, string messageAr, string? url = null);
        Task<Result<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId, bool? isUnread = null);
        Task<Result> MarkAsReadAsync(Guid userId, Guid notificationId);
        Task<Result> MarkAllAsReadAsync(Guid userId);
    }
}
