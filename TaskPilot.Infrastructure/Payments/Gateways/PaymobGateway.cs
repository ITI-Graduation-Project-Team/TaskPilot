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
                
                var authRequest = new 
                { 
                    username = _options.Username,
                    password = _options.Password,
                    source = "merchant"
                };
                var authResponse = await client.PostAsync($"{_baseUrl}/auth/tokens", new StringContent(JsonSerializer.Serialize(authRequest), System.Text.Encoding.UTF8, "application/json"), ct);
                var authContent = await authResponse.Content.ReadAsStringAsync(ct);
                if (!authResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Paymob {Step} failed. " +
                        "StatusCode: {StatusCode}. " +
                        "Response: {Response}",
                        "GetAuthToken",
                        (int)authResponse.StatusCode,
                        authContent);
                    throw new InvalidOperationException(
                        $"Paymob error ({(int)authResponse.StatusCode}): {authContent}");
                }
                var token = JsonDocument.Parse(authContent).RootElement.GetProperty("token").GetString();

                if (!_options.PlanMappings.TryGetValue(planId, out var mapping))
                {
                    _logger.LogError(
                        "Paymob: No mapping found for planId '{PlanId}'. " +
                        "Available keys: {Keys}",
                        planId,
                        string.Join(", ", _options.PlanMappings.Keys));
                    return new GatewaySubscriptionResult { Status = "failed", ErrorMessage = $"No mapping found for plan '{planId}' in Paymob settings." };
                }

                var amountCents = interval == BillingCycle.Monthly ? mapping.MonthlyAmountCents : mapping.AnnualAmountCents;

                var orderRequest = new
                {
                    auth_token = token,
                    delivery_needed = "false",
                    amount_cents = amountCents.ToString(),
                    currency = "EGP",
                    items = Array.Empty<object>()
                };

                var orderResponse = await client.PostAsync($"{_baseUrl}/ecommerce/orders", new StringContent(JsonSerializer.Serialize(orderRequest), System.Text.Encoding.UTF8, "application/json"), ct);
                var orderContent = await orderResponse.Content.ReadAsStringAsync(ct);
                if (!orderResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Paymob {Step} failed. " +
                        "StatusCode: {StatusCode}. " +
                        "Response: {Response}",
                        "CreateOrder",
                        (int)orderResponse.StatusCode,
                        orderContent);
                    throw new InvalidOperationException(
                        $"Paymob error ({(int)orderResponse.StatusCode}): {orderContent}");
                }
                var orderId = JsonDocument.Parse(orderContent).RootElement.GetProperty("id").GetInt32();

                var integrationId = mapping.IntegrationId;
                if (!int.TryParse(integrationId, out var integrationIdInt))
                {
                    _logger.LogError(
                        "Paymob IntegrationId '{Id}' is not a valid integer",
                        integrationId);
                    throw new InvalidOperationException(
                        $"Paymob IntegrationId '{integrationId}' " +
                        "is not a valid integer. Check appsettings.json.");
                }

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
                    integration_id = integrationIdInt
                };

                var paymentKeyResponse = await client.PostAsync($"{_baseUrl}/acceptance/payment_keys", new StringContent(JsonSerializer.Serialize(paymentKeyRequest), System.Text.Encoding.UTF8, "application/json"), ct);
                var paymentKeyContent = await paymentKeyResponse.Content.ReadAsStringAsync(ct);
                if (!paymentKeyResponse.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Paymob {Step} failed. " +
                        "StatusCode: {StatusCode}. " +
                        "Response: {Response}",
                        "GetPaymentKey",
                        (int)paymentKeyResponse.StatusCode,
                        paymentKeyContent);
                    throw new InvalidOperationException(
                        $"Paymob error ({(int)paymentKeyResponse.StatusCode}): {paymentKeyContent}");
                }
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
                _logger.LogError(ex,
                    "Paymob CreateSubscriptionAsync failed: {Message}",
                    ex.Message);
                return new GatewaySubscriptionResult
                {
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    ClientSecret = null
                };
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
