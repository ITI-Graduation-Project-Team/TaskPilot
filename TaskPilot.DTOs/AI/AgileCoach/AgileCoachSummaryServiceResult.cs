using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.AI.AgileCoach
{
    public class AgileCoachSummaryResponse
    {
        public Guid Id { get; set; }
        public Guid TaskItemId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public bool IsNewlyGenerated { get; set; }
    }

    public class AgileCoachSummaryServiceResult
    {
        public AgileCoachSummaryResponse Summary { get; set; } = null!;
    }
}
