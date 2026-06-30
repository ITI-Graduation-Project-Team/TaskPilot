using System;

namespace TaskPilot.AI.Models.ContextAdvisor
{
    public class ContextAdvisorAskRequest
    {
        public Guid TaskId { get; set; }
        
        public Guid? ConversationId { get; set; }
        
        public string Question { get; set; } = string.Empty;
    }
}
