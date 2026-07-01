namespace TaskPilot.Models.Gateways
{
    public class WebhookParseResult
    {
        public bool IsValid { get; set; }
        public string? EventType { get; set; }
        public string? SubscriptionId { get; set; }
        public string? CustomerId { get; set; }
        public string? PaymentId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
