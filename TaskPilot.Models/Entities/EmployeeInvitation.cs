using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class EmployeeInvitation
        : AuditableEntity<Guid>
    {
        public string Email { get; set; } = string.Empty;

        public Guid CompanyId { get; set; }

        public Company Company { get; set; } = null!;

        public Guid InvitedById { get; set; }

        public ProjectManager InvitedBy { get; set; }
            = null!;

        public string Token { get; set; }
            = Guid.NewGuid().ToString();

        public DateTime ExpiresAt { get; set; }

        public bool IsAccepted { get; set; } = false;
    }
}
