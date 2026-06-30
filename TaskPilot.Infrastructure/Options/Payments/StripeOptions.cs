namespace TaskPilot.Infrastructure.Options.Payments
{
    public class StripeOptions
    {
        public const string SectionName = "PaymentGateways:Stripe";
        
        public string SecretKey { get; set; } = string.Empty;
        public string PublishableKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = "2024-04-10";
        public System.Collections.Generic.Dictionary<string, PlanMappingOptions> PlanMappings { get; set; } = new();
    }
}
