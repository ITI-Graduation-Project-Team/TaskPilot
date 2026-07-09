namespace TaskPilot.Infrastructure.Options.Payments
{
    public class PaymobPlanMapping
    {
        public string IntegrationId { get; set; } = string.Empty;
        public int MonthlyAmountCents { get; set; }
        public int AnnualAmountCents { get; set; }
    }
}
