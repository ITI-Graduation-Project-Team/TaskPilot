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
        public Guid ProjectManagerId { get; set; }
        public Guid UserSubscriptionId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public PaymentStatus Status { get; set; }
        public PaymentGateway PaymentGateway { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime? PaidAt { get; set; }
        public ProjectManager ProjectManager { get; set; } = null!;
        public UserSubscription Subscription { get; set; } = null!;
    }
}
