using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI
{
    public class AiChatMessageDto
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int SequenceIndex { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class AiChatResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public int CompletenessScore { get; set; }
        public List<string> AnsweredQuestions { get; set; } = new List<string>();
        public bool IsReadyToGenerate { get; set; }
    }

    public class BrdUploadResultDto
    {
        public Guid ProjectId { get; set; }
        public string ExtractedText { get; set; } = string.Empty;
        public List<string> DetectedGaps { get; set; } = new List<string>();
        public int CompletenessScore { get; set; }
    }

    public class GenerateProjectDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }

    public class ProjectChatHistoryDto
    {
        public Guid ProjectId { get; set; }
        public List<AiChatMessageDto> Messages { get; set; } = new List<AiChatMessageDto>();
    }

    public class SendAiMessageDto
    {
        public Guid? ProjectId { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<AiChatMessageDto> ChatHistory { get; set; } = new List<AiChatMessageDto>();
    }
}
