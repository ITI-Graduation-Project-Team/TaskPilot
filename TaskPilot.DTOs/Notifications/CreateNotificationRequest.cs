using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Notifications
{
    public sealed class CreateNotificationRequest
    {
        public Guid UserId { get; init; }
        public NotificationType Type { get; init; }
        public string MessageEn { get; init; } = string.Empty;
        public string MessageAr { get; init; } = string.Empty;
        public string? Url { get; init; }
    }
}
