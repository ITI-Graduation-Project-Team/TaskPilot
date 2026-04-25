using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        public int PmId { get; set; }
        public int SubscriptionId { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public PaymentStatus Status { get; set; }
        public string PaymentProvider { get; set; } = "Stripe";
        public string? TransactionId { get; set; }
        public string? InvoiceUrl { get; set; }

        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User ProjectManager { get; set; } = null!;
        public UserSubscription Subscription { get; set; } = null!;
    }
}
