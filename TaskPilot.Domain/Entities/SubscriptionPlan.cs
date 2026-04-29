using TaskPilot.Domain.Common;
namespace TaskPilot.Domain.Entities
{
    public class SubscriptionPlan : AuditableEntity<int>
    {
         public string Name { get; set; } = string.Empty;

    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }

    public string Currency { get; set; } = "EGP";

    public int MaxProjects { get; set; }
    public int MaxUsersPerProject { get; set; }

    public bool HasAi { get; set; }
    public bool HasAdvancedAnalytics { get; set; }

    public bool HasTrial { get; set; }
    public int? TrialDays { get; set; }

    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
    }
}