using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class TaskComment : AuditableEntity<Guid>
    {
        public Guid TaskId { get; set; }
        public string ContentEn { get; set; } = string.Empty;
        public string ContentAr { get; set; } = string.Empty;

        public Guid? UserId { get; set; } 
        public Task Task { get; set; } = null!;
    }
}
