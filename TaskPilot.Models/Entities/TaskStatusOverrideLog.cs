using System;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Entities
{
    public class TaskStatusOverrideLog : AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public TaskItem Task { get; set; } = null!;

        public Guid PerformedByPmId { get; set; } 

        public TaskItemStatus FromStatus { get; set; }
        public TaskItemStatus ToStatus { get; set; }

        public string ReasonEn { get; set; } = string.Empty;
        public string? ReasonAr { get; set; }

        /// <summary>
        /// "ReviewReject" or "Reopen"
        /// </summary>
        public string OverrideType { get; set; } = string.Empty;
    }
}
