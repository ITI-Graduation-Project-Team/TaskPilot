using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class SuggestedSlotDto
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
