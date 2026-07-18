using System;
using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.Chat
{
    public class SendChatMessageDto
    {
        [Required]
        public string Message { get; set; } = string.Empty;
    }
}
