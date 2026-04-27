namespace TaskPilot.Domain.Entities
{
    public class ProjectManager : User
    {
        public string? CompanyName { get; set; }
        public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();




    }
}
