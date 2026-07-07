namespace TaskPilot.Infrastructure.Options.Payments
{
    public class PaymobOptions
    {
        public const string SectionName = "Paymob";
        public string ApiKey { get; set; } = string.Empty;
        public string IntegrationId { get; set; } = string.Empty;
        public string IframeId { get; set; } = string.Empty;
        public string HmacSecret { get; set; } = string.Empty;
        public System.Collections.Generic.Dictionary<string, PlanMappingOptions> PlanMappings { get; set; } = new();
    }
}
