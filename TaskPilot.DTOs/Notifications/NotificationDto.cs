using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Notifications
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string MessageEn { get; set; } = string.Empty;
        public string MessageAr { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string? Url { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
