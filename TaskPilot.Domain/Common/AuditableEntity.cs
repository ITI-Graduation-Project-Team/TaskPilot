namespace TaskPilot.Domain.Common
{
    public abstract class AuditableEntity<TId> : BaseEntity<TId>
    {
       
        public DateTime CreatedAt { get; protected set; }
        public Guid? CreatedBy { get; protected set; }

        public DateTime? ModifiedAt { get; protected set; }
        public Guid? ModifiedBy { get; protected set; }

        public bool IsDeleted { get; protected set; }

        public bool IsActive => !IsDeleted;

        protected void Initialize(Guid? userId = null)
        {
            CreatedAt = DateTime.UtcNow;
            CreatedBy = userId;
        }

        public void SetUpdated(Guid? userId = null)
        {
            ModifiedAt = DateTime.UtcNow;
            ModifiedBy = userId;
        }

        public void Delete(Guid? userId = null)
        {
            if (IsDeleted)
                return;

            IsDeleted = true;
            SetUpdated(userId);
        }

        public void Restore(Guid? userId = null)
        {
            if (!IsDeleted)
                return;

            IsDeleted = false;
            SetUpdated(userId);
        }
    }
}
