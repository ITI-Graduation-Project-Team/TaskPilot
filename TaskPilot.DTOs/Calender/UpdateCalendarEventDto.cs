using System;
using System.Text.Json.Serialization;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Calender
{
    public class UpdateCalendarEventDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public int? DurationInMinutes { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CalenderEventType? EventType { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TaskPriority? Priority { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TaskItemStatus? Status { get; set; }
    }
}
