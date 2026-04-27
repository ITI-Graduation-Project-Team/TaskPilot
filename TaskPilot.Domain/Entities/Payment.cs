using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Domain.Common;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Domain.Entities
{
    public class Payment : AuditableEntity<Guid>
    {
        public string? GatewayTransactionId { get; set; }
        public Guid UserId { get; set; }
        public int UserSubscriptionId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public PaymentStatus Status { get; set; }
        public string PaymentGateway { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public User ProjectManager { get; set; } = null!;
        public UserSubscription Subscription { get; set; } = null!;
    }
}
