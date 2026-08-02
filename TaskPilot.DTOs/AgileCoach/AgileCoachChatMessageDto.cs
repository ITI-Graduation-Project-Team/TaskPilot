using System;

namespace TaskPilot.DTOs.AgileCoach
{
    public class AgileCoachChatMessageDto
    {
        public Guid Id { get; set; }
        public string Role { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string Lang { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
