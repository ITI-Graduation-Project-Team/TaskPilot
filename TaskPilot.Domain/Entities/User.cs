namespace TaskPilot.Domain.Entities
{
    using System.ComponentModel.DataAnnotations;
    using TaskPilot.Domain.Common;

    public class User : AuditableEntity
    {
        [Required]
        public string FirstNameEn { get; set; } = string.Empty;
        [Required]
        public string LastNameEn { get; set; } = string.Empty;
        [Required]
        public string FirstNameAr { get; set; } = string.Empty;
        [Required]
        public string LastNameAr { get; set; } = string.Empty;
        [Required]
        public string Email { get; set; } = string.Empty;
        public ICollection<Notification> Notifications { get; set; }

    }
}
