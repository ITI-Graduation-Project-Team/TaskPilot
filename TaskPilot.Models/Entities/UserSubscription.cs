using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;
namespace TaskPilot.Models.Entities
{
    public class UserSubscription : AuditableEntity<Guid>
    {
        public Guid ProjectManagerId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public BillingCycle BillingCycle { get; set; }
        public SubscriptionStatus Status { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsTrial { get; set; }
        public DateTime? TrialEndDate { get; set; }
        
        public string? GatewaySubscriptionId { get; set; }
        public string? GatewayCustomerId { get; set; }
        public PaymentGateway Gateway { get; set; }
        public DateTime? CanceledAt { get; set; }

        public ProjectManager ProjectManager { get; set; } = null!;
        public SubscriptionPlan Plan { get; set; } = null!;
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
