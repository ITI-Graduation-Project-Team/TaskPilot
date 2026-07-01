using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Gateways;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Interfaces.Payments
{
    public interface IPaymentGateway
    {
        string ProviderName { get; }
        TaskPilot.Models.Enums.PaymentGateway GatewayType { get; }
        Task<GatewaySubscriptionResult> CreateSubscriptionAsync(
            string customerId, string planId, BillingCycle interval, 
            string paymentMethodId, string idempotencyKey, 
            string? returnUrl, string? cancelUrl, CancellationToken ct);
        Task<GatewayCancelResult> CancelSubscriptionAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct);
        Task<Result<string>> CreateOrGetCustomerAsync(
            string userId, string email, CancellationToken ct);
        Task<WebhookParseResult> ParseAndVerifyWebhookAsync(
            string payload, IHeaderDictionary headers, CancellationToken ct);
    }
}
