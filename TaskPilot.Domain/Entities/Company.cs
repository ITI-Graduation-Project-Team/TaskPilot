using TaskPilot.Domain.Common;

namespace TaskPilot.Domain.Entities
{
    public class Company : AuditableEntity<Guid>
    {
        public string Name { get; set; } = string.Empty;

        public Guid OwnerId { get; set; }
        public ProjectManager Owner { get; set; } = null!;

        public ICollection<User> Users { get; set; } = new List<User>();

        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}