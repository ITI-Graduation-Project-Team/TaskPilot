using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class WorkloadSummaryDto
    {
        public int WorkingMinutes { get; set; }

        public int ScheduledMinutes { get; set; }

        public int avaliableMinutes { get; set; }

        public int OverbookedMinutes { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
