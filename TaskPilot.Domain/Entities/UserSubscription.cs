namespace TaskPilot.Domain.Entities
{
    public class UserSubscription
    {
        public int Id { get; set; }
        public int PmId { get; set; }
        public int PlanId { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? TrialEndDate { get; set; }

        public bool IsActive { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public string? StripeCustomerId { get; set; }
        public string? StripeSubscriptionId { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; set; }
        public ProjectManager ProjectManager { get; set; } = null!;
        public SubscriptionPlan Plan { get; set; } = null!;
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
