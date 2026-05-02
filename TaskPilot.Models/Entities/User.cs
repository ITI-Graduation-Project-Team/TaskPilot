namespace TaskPilot.Models.Entities
{
    using Microsoft.AspNetCore.Identity;
    using System.Security.Cryptography;
    using TaskPilot.Models.Common;

    public class User : IdentityUser<Guid>
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Guid? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public bool IsActive => !IsDeleted;

        public string FirstNameEn { get; set; } = string.Empty;
        public string LastNameEn { get; set; } = string.Empty;
        public string FirstNameAr { get; set; } = string.Empty;
        public string LastNameAr { get; set; } = string.Empty;
        public Guid? CompanyId { get; set; }
        public Company Company { get; set; } = null!;
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    }
}
