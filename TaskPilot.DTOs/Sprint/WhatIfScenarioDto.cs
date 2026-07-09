using System;

namespace TaskPilot.DTOs.Sprint
{
    public class WhatIfScenarioDto
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string ProjectedImpactEn { get; set; } = string.Empty;
        public string ProjectedImpactAr { get; set; } = string.Empty;
        
        public WhatIfActionDto SuggestedAction { get; set; } = new();
    }

    public class WhatIfActionDto
    {
        public string ActionType { get; set; } = string.Empty; // "Reassign" | "DropScope" | "ExtendSprint"
        public Guid? TargetTaskId { get; set; }
        public Guid? SuggestedEmployeeId { get; set; }
        public int? ExtensionDays { get; set; }
        public Guid? StoryToDropId { get; set; }
    }
}
