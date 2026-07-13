using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class WorkloadResponseDto
    {
        public WorkloadSummaryDto Summary { get; set; } = new();

        public WorkloadBreakdownDto Breakdown { get; set; } = new();

        public List<TimelineEventDto> Timeline { get; set; } = [];

        public List<WeekOverviewDto> WeekOverview { get; set; } = [];

        public QuickStatsDto QuickStats { get; set; } = new();

        public List<SuggestedSlotDto> SuggestedSlots { get; set; } = [];
    }
}
