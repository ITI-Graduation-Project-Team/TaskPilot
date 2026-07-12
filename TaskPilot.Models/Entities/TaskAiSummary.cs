using TaskPilot.Models.Common;
using System;

namespace TaskPilot.Models.Entities
{
    public class TaskAiSummary : AuditableEntity<Guid>
    {
        public Guid TaskItemId { get; set; }
        
        public string ContentEn { get; set; } = string.Empty;
        public string ContentAr { get; set; } = string.Empty;
        
        // Serialized List<CitationDto> — read/written as atomic unit with summary
        public string CitationsJson { get; set; } = string.Empty;
        
        public DateTime GeneratedAt { get; set; }

        public TaskItem TaskItem { get; set; } = null!;
    }
}
