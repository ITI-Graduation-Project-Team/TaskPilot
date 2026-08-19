using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskPilot.Data.Context;
using TaskPilot.DTOs.Notifications;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationNotifier _notifier;

        public NotificationService(ApplicationDbContext context, INotificationNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        public async Task<Result> SendAsync(Guid userId, NotificationType type, string messageEn, string messageAr, string? url = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                MessageEn = messageEn,
                MessageAr = messageAr,
                Url = url,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();

            var dto = new NotificationDto
            {
                Id = notification.Id,
                MessageEn = notification.MessageEn,
                MessageAr = notification.MessageAr,
                Type = notification.Type,
                IsRead = notification.IsRead,
                Url = notification.Url,
                CreatedAt = notification.CreatedAt
            };

            await _notifier.NotifyAsync(userId, dto);
            return Result.Success();
        }

        public async Task<Result> SendManyAsync(
            IReadOnlyCollection<CreateNotificationRequest> requests,
            CancellationToken cancellationToken = default)
        {
            if (requests.Count == 0)
            {
                return Result.Success();
            }

            var notifications = requests.Select(request => new Notification
            {
                UserId = request.UserId,
                Type = request.Type,
                MessageEn = request.MessageEn,
                MessageAr = request.MessageAr,
                Url = request.Url,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _context.Notifications.AddRangeAsync(notifications, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await Task.WhenAll(notifications.Select(notification =>
                _notifier.NotifyAsync(notification.UserId, new NotificationDto
                {
                    Id = notification.Id,
                    MessageEn = notification.MessageEn,
                    MessageAr = notification.MessageAr,
                    Type = notification.Type,
                    IsRead = notification.IsRead,
                    Url = notification.Url,
                    CreatedAt = notification.CreatedAt
                })));

            return Result.Success();
        }

        public async Task<Result<List<NotificationDto>>> GetUserNotificationsAsync(Guid userId, bool? isUnread = null)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId);

            if (isUnread.HasValue && isUnread.Value)
            {
                query = query.Where(n => !n.IsRead);
            }

            var list = await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    MessageEn = n.MessageEn,
                    MessageAr = n.MessageAr,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    Url = n.Url,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return Result<List<NotificationDto>>.Success(list);
        }

        public async Task<Result> MarkAsReadAsync(Guid userId, Guid notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return Result.Failure(NotificationErrors.NotFound());
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Result.Success();
        }

        public async Task<Result> MarkAllAsReadAsync(Guid userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            if (unreadNotifications.Any())
            {
                await _context.SaveChangesAsync();
            }

            return Result.Success();
        }
    }
}
