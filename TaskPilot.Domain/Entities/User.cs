namespace TaskPilot.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;
    using System.Xml.Linq;
    using TaskPilot.Domain.Common;

    public class User : AuditableEntity<Guid>
    {
        public string FirstNameEn { get; set; } = string.Empty;
        public string LastNameEn { get; set; } = string.Empty;
        public string FirstNameAr { get; set; } = string.Empty;
        public string LastNameAr { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
        public Company Company { get; set; } = null!;
        public Guid ApplicationUserId { get; set; }

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    }
}
