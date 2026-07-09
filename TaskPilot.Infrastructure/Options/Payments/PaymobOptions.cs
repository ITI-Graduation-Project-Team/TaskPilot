namespace TaskPilot.Infrastructure.Options.Payments
{
    public class PaymobOptions
    {
        public const string SectionName = "PaymentGateways:Paymob";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string IntegrationId { get; set; } = string.Empty;
        public string IframeId { get; set; } = string.Empty;
        public string HmacSecret { get; set; } = string.Empty;
        public System.Collections.Generic.Dictionary<string, PaymobPlanMapping> PlanMappings { get; set; } = new();
    }
}
