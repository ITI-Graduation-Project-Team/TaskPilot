using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class CalendarDashboardResponseDto
    {
        public List<CalendarBlockDto> Events { get; set; } = new();

        public int WorkingHours { get; set; } 
        public int ScheduledHours { get; set; } 
        public int FreeSlots { get; set; }
        public string WorkloadStatus{ get; set; }
        public List<string> AiSuggestions { get; set; } = new();
    }
}
