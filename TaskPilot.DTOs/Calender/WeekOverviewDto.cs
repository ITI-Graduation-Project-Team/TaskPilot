using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class WeekOverviewDto
    {
            public DateOnly Date { get; set; }

            public int ScheduledMinutes { get; set; }

            public int CapacityMinutes { get; set; }

            public bool IsOverloaded { get; set; }
     }
    
}
