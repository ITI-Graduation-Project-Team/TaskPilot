using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.AgileCoach
{
    public class AgileCoachSummaryResponse
    {
        public Guid Id { get; set; }
        public Guid TaskItemId { get; set; }
        public string CodebaseNotes { get; set; } = string.Empty;
        public string RelatedPastTasks { get; set; } = string.Empty;
        public string TechStackContext { get; set; } = string.Empty;
        public string SuggestedImplementationGuidance { get; set; } = string.Empty;
        public List<CitationDto> Citations { get; set; } = new List<CitationDto>();
        public DateTime GeneratedAt { get; set; }
        public bool IsNewlyGenerated { get; set; }
    }

    public class AgileCoachSummaryServiceResult
    {
        public AgileCoachSummaryResponse Summary { get; set; } = null!;
    }
}
