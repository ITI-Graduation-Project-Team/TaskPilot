using System;

namespace TaskPilot.DTOs.AI.Requirements
{
    public sealed class FinalizeRequirementsRequest
    {
        public Guid CompanyId { get; set; }
        public string ProjectNameEn { get; set; } = string.Empty;
        public string? ProjectNameAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public int SprintDurationInDays { get; set; } = 14;
        public decimal TargetSprintHours { get; set; } = 80;
    }

    public class FinalizeRequirementsResponse
    {
        public Guid ProjectId { get; set; }
        public Guid CompanyId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool RequirementsFinalized { get; set; }
    }
}
