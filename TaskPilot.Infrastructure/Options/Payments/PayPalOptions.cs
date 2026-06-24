namespace TaskPilot.Infrastructure.Options.Payments
{
    public class PayPalOptions
    {
        public const string SectionName = "PaymentGateways:PayPal";
        
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Mode { get; set; } = "sandbox"; // or "live"
        public string WebhookId { get; set; } = string.Empty;
        public System.Collections.Generic.Dictionary<string, PlanMappingOptions> PlanMappings { get; set; } = new();
    }
}
