namespace TaskPilot.Models.Common
{
    public abstract class AuditableEntity<TId> : BaseEntity<TId>
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public bool IsActive => !IsDeleted;
    }
    public abstract class AuditableEntity : AuditableEntity<Guid>
    {
    }
}
