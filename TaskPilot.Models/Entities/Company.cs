using TaskPilot.Models.Common;

namespace TaskPilot.Models.Entities
{
    public class Company : AuditableEntity<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? CloudinaryPublicId { get; set; }
        public Guid OwnerId { get; set; }
        public ProjectManager Owner { get; set; } = null!;
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
        public ICollection<Policy> Policies { get; set; } = new List<Policy>();
        public ICollection<EmployeeInvitation> Invitations
        { get; set; }
                   = new List<EmployeeInvitation>();
    }
}