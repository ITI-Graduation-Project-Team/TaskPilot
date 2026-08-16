namespace TaskPilot.Models.Entities
{
    public class ProjectManager : User
    {
        public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public long CurrentStorageUsedBytes { get; set; } = 0;
        public long CurrentTokensUsedThisMonth { get; set; } = 0;
    }
}
