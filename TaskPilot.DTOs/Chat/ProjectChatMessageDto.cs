using System;

namespace TaskPilot.DTOs.Chat
{
    public class ProjectChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int SequenceIndex { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
}
