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
        public ICollection<EmployeeInvitation> Invitations { get; set; } = new List<EmployeeInvitation>();

        public decimal WorkingHoursPerDay { get; set; } = 8.0m;
        public int WorkingDaysMask { get; set; } = 62; // Default to Mon-Fri (2+4+8+16+32 = 62)
        public decimal DefaultCapacityBufferPercentage { get; set; } = 0.80m;
    }
}