using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class QuickStatsDto
    {
        public double AverageDailyLoadHours { get; set; }

        public int FreeMinutesThisWeek { get; set; }

        public int MeetingMinutes { get; set; }

        public int FocusMinutes { get; set; }
    }
}
