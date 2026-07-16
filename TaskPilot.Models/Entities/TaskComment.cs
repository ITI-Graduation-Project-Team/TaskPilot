using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class TaskComment : AuditableEntity<Guid>
    {
        public Guid? TaskId { get; set; }
        public Guid? UserId { get; set; }

        /// <summary>
        /// Comment content — single field supporting Arabic or English text.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        public TaskItem Task { get; set; } = null!;
        public User? User { get; set; }
    }
}
