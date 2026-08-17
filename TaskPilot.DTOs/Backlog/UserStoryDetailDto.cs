using System;

namespace TaskPilot.DTOs.Backlog
{
    public class UserStoryDetailDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        public string Priority { get; set; } = string.Empty;
    }
}
