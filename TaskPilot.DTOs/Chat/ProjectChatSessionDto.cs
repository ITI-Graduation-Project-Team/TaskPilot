using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Chat
{
    public class ProjectChatSessionDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string? BrdExtractedText { get; set; }
        public List<ProjectChatMessageDto> Messages { get; set; } = new List<ProjectChatMessageDto>();
    }
}
