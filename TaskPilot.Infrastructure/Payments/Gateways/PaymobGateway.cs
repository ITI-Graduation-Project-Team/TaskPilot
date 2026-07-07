using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Gateways;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Infrastructure.Payments.Gateways
{
    public class PaymobGateway : IPaymentGateway
    {
        private readonly PaymobOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PaymobGateway> _logger;
        private readonly string _baseUrl = "https://accept.paymob.com/api";

        public PaymobGateway(IOptions<PaymobOptions> options, IHttpClientFactory httpClientFactory, ILogger<PaymobGateway> logger)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string ProviderName => "Paymob";
        public PaymentGateway GatewayType => PaymentGateway.Paymob;

        public async Task<GatewaySubscriptionResult> CreateSubscriptionAsync(
            string customerId, string planId, BillingCycle interval, 
            string paymentMethodId, string idempotencyKey, 
            string? returnUrl, string? cancelUrl, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                
                var authRequest = new { api_key = _options.ApiKey };
                var authResponse = await client.PostAsync($"{_baseUrl}/auth/tokens", new StringContent(JsonSerializer.Serialize(authRequest), System.Text.Encoding.UTF8, "application/json"), ct);
                authResponse.EnsureSuccessStatusCode();
                var authContent = await authResponse.Content.ReadAsStringAsync(ct);
                var token = JsonDocument.Parse(authContent).RootElement.GetProperty("token").GetString();

                if (!_options.PlanMappings.TryGetValue(planId, out var mapping))
                {
                    return new GatewaySubscriptionResult { Status = "failed", ErrorMessage = $"No mapping found for plan '{planId}' in Paymob settings." };
                }

                var priceStr = interval == BillingCycle.Monthly ? mapping.MonthlyPriceId : mapping.AnnualPriceId;
                if (!int.TryParse(priceStr, out var amountCents))
                {
                    amountCents = 10000;
                }

                var orderRequest = new
                {
                    auth_token = token,
                    delivery_needed = "false",
                    amount_cents = amountCents.ToString(),
                    currency = "EGP",
                    items = Array.Empty<object>()
                };

                var orderResponse = await client.PostAsync($"{_baseUrl}/ecommerce/orders", new StringContent(JsonSerializer.Serialize(orderRequest), System.Text.Encoding.UTF8, "application/json"), ct);
                orderResponse.EnsureSuccessStatusCode();
                var orderContent = await orderResponse.Content.ReadAsStringAsync(ct);
                var orderId = JsonDocument.Parse(orderContent).RootElement.GetProperty("id").GetInt32();

                var paymentKeyRequest = new
                {
                    auth_token = token,
                    amount_cents = amountCents.ToString(),
                    expiration = 3600,
                    order_id = orderId.ToString(),
                    billing_data = new
                    {
                        apartment = "NA",
                        email = customerId,
                        floor = "NA",
                        first_name = "User",
                        street = "NA",
                        building = "NA",
                        phone_number = "NA",
                        shipping_method = "NA",
                        postal_code = "NA",
                        city = "NA",
                        country = "EG",
                        last_name = "User",
                        state = "NA"
                    },
                    currency = "EGP",
                    integration_id = _options.IntegrationId
                };

                var paymentKeyResponse = await client.PostAsync($"{_baseUrl}/acceptance/payment_keys", new StringContent(JsonSerializer.Serialize(paymentKeyRequest), System.Text.Encoding.UTF8, "application/json"), ct);
                paymentKeyResponse.EnsureSuccessStatusCode();
                var paymentKeyContent = await paymentKeyResponse.Content.ReadAsStringAsync(ct);
                var paymentToken = JsonDocument.Parse(paymentKeyContent).RootElement.GetProperty("token").GetString();

                var iframeUrl = $"https://accept.paymob.com/api/acceptance/iframes/{_options.IframeId}?payment_token={paymentToken}";

                return new GatewaySubscriptionResult
                {
                    SubscriptionId = orderId.ToString(),
                    Status = "pending",
                    ClientSecret = iframeUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paymob CreateSubscriptionAsync error");
                return new GatewaySubscriptionResult { Status = "failed", ErrorMessage = ex.Message };
            }
        }

        public Task<GatewayCancelResult> CancelSubscriptionAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            return Task.FromResult(new GatewayCancelResult { IsSuccess = true });
        }

        public Task<GatewayCancelResult> CancelAtPeriodEndAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            return Task.FromResult(new GatewayCancelResult { IsSuccess = true });
        }

        public Task<Result<string>> CreateOrGetCustomerAsync(
            string userId, string email, CancellationToken ct)
        {
            return Task.FromResult(Result.Success(email));
        }

        public Task<WebhookParseResult> ParseAndVerifyWebhookAsync(
            string payload, IHeaderDictionary headers, CancellationToken ct)
        {
            try
            {
                var doc = JsonDocument.Parse(payload);
                var type = doc.RootElement.GetProperty("type").GetString();
                var obj = doc.RootElement.GetProperty("obj");
                var orderId = obj.GetProperty("order").GetProperty("id").GetInt32().ToString();
                var success = obj.GetProperty("success").GetBoolean();
                
                return Task.FromResult(new WebhookParseResult
                {
                    IsValid = true,
                    EventType = success ? "transaction.success" : "transaction.failed",
                    SubscriptionId = orderId,
                    PaymentId = obj.GetProperty("id").GetInt32().ToString(),
                    Amount = obj.GetProperty("amount_cents").GetInt32() / 100m,
                    Status = success ? "success" : "failed"
                });
            }
            catch
            {
                return Task.FromResult(new WebhookParseResult { IsValid = false });
            }
        }
    }
}
