using System;
using System.Collections.Generic;

namespace TaskPilot.AI.Models.Sprint
{
    public class WhatIfResult
    {
        public List<WhatIfScenario> Scenarios { get; set; } = new();
    }

    public class WhatIfScenario
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string ProjectedImpactEn { get; set; } = string.Empty;
        public string ProjectedImpactAr { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public Guid? TargetTaskId { get; set; }
        public Guid? SuggestedEmployeeId { get; set; }
        public int? ExtensionDays { get; set; }
        public Guid? StoryToDropId { get; set; }
    }
}
