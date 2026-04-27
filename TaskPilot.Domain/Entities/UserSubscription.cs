using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class UserSubscription : AuditableEntity<int>
    {
        public int UserId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }
        public bool AutoRenew { get; set; }
        public bool IsTrial { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public User ProjectManager { get; set; } = null!;
        public SubscriptionPlan Plan { get; set; } = null!;
        public ICollection<Payment>? Payments { get; set; }


        //public bool IsActive { get; set; }
        //public bool CancelAtPeriodEnd { get; set; }
        //public string? StripeCustomerId { get; set; }
        //public string? StripeSubscriptionId { get; set; }
        //public bool IsDeleted { get; set; } = false;
        //public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        //public DateTime? ModifiedAt { get; set; }
    }
}
