using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Calender
{
    public class CreateCalendarEventDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        //public DateTime EndDate { get; set; }
        public int DurationInMinutes {  get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CalenderEventType EventType { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TaskPriority Priority { get; set; }
    }
}
