using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.AgileCoach
{
    public class AgileCoachChatRequest
    {
        public Guid TaskItemId { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ChatMessageDto> History { get; set; } = new List<ChatMessageDto>();
    }
}
