using System;

namespace TaskPilot.DTOs.UserSubscriptions
{
    public class UserSubscriptionDto
    {
        public Guid Id { get; set; }
        public Guid ProjectManagerId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string BillingCycle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool AutoRenew { get; set; }
        public bool IsTrial { get; set; }
        public DateTime? TrialEndDate { get; set; }
        
        public string? ClientSecret { get; set; }
    }
}
