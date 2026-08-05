using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Planning
{
    public class SprintSelectionResult
    {
        public List<SuggestedStoryDto> SelectedStories { get; set; } = new();
        public List<ExcludedStoryDto> ExcludedStories { get; set; } = new();
        public decimal UtilizedHours { get; set; }
        public decimal TargetHours { get; set; }
        public decimal UtilizationPercent => TargetHours == 0 ? 0 : Math.Round(UtilizedHours / TargetHours * 100, 2);
    }
}
