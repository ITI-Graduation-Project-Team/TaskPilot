using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class CalendarBlockDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string EventType { get; set; } = string.Empty; 
        public string Priority { get; set; } = string.Empty;  
        public string Status { get; set; } = string.Empty;   
        public Guid? RelatedTaskId { get; set; }
    }
}
