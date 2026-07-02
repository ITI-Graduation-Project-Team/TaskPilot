namespace TaskPilot.Models.Gateways
{
    public class GatewaySubscriptionResult
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSetupIntent { get; set; } = false;
    }
}
