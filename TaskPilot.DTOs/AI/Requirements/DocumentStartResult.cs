using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.Requirements
{
    public class DocumentStartResult
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsLimitedMode { get; set; }
        public object ConfidenceScores { get; set; } = new List<object>();
        public object PendingQuestions { get; set; } = new List<object>();
        public string Message { get; set; } = string.Empty;
    }
}
