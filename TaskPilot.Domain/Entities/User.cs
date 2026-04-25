namespace TaskPilot.Domain.Entities
{
    using TaskPilot.Domain.Common;
    using TaskPilot.Domain.Enums;

    public class User : AuditableEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public float? HistoricalVelocity { get; set; }
        public Availability? AvailabilityStatus { get; set; }
        public float? MaxSprintHours { get; set; }
        public string? CompanyName { get; set; }
        public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
        public ICollection<Task> AssignedTasks { get; set; } = new List<Task>();
        public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
