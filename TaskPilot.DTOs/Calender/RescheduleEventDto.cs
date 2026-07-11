using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public  class RescheduleEventDto
    {
        public DateTime NewStart { get; set; }
        public DateTime NewEnd { get; set; }
    }
}
