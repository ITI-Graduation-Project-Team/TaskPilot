using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class CalenderEvent : AuditableEntity
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public Guid EmployeeId { get; set; }
        public Employee Employee { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TaskPriority TaskPriority { get; set; }
        public CalenderEventType Type { get; set; }
        public TaskItemStatus Status { get; set; }
        public TaskItem? RelatedTask { get; set; }
        public Guid? RelatedTaskId { get; set; }

    }
}
