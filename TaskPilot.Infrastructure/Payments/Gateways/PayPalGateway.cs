using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Gateways;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Infrastructure.Payments.Gateways
{
    public class PayPalGateway : IPaymentGateway
    {
        private readonly PayPalOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PayPalGateway> _logger;
        private readonly string _baseUrl;

        public PayPalGateway(IOptions<PayPalOptions> options, IHttpClientFactory httpClientFactory, ILogger<PayPalGateway> logger)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = _options.Mode.Equals("live", StringComparison.OrdinalIgnoreCase)
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";
        }

        public string ProviderName => "PayPal";
        public PaymentGateway GatewayType => PaymentGateway.PayPal;

        private async Task<string> GetAccessTokenAsync(HttpClient client)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" }
            });
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            return json.RootElement.GetProperty("access_token").GetString()!;
        }

        public async Task<GatewaySubscriptionResult> CreateSubscriptionAsync(
            string customerId, string planId, BillingCycle interval,
            string paymentMethodId, string idempotencyKey,
            string? returnUrl, string? cancelUrl, CancellationToken ct)
        {
            if (!_options.PlanMappings.TryGetValue(planId, out var mapping))
            {
                return new GatewaySubscriptionResult { Status = "failed", ErrorMessage = $"No mapping found for plan '{planId}' in PayPal settings." };
            }

            var mappedPlanId = interval == BillingCycle.Monthly ? mapping.MonthlyPriceId : mapping.AnnualPriceId;
            if (string.IsNullOrEmpty(mappedPlanId))
            {
                return new GatewaySubscriptionResult { Status = "failed", ErrorMessage = $"No PayPal plan ID configured for plan '{planId}' ({interval})." };
            }

            _logger.LogInformation("Attempting to create PayPal subscription for plan {PlanId}", planId);

            try
            {
                var client = _httpClientFactory.CreateClient();
                var accessToken = await GetAccessTokenAsync(client);

                var requestBody = new
                {
                    plan_id = mappedPlanId,
                    subscriber = new { custom_id = customerId },
                    application_context = new
                    {
                        return_url = returnUrl,
                        cancel_url = cancelUrl
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/billing/subscriptions");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("PayPal-Request-Id", idempotencyKey);
                request.Headers.Add("Prefer", "return=representation");
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("PayPal API error during CreateSubscriptionAsync: {Message}", content);
                    return new GatewaySubscriptionResult
                    {
                        Status = "failed",
                        ErrorMessage = $"PayPal API error: {content}",
                        ClientSecret = null
                    };
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<PayPalSubscriptionResponse>(content, options);
                var id = result?.Id;
                var status = result?.Status;
                var approveLink = result?.Links?.FirstOrDefault(l => l.Rel.Equals("approve", StringComparison.OrdinalIgnoreCase))?.Href;

                _logger.LogInformation("Successfully created PayPal subscription {GatewaySubscriptionId}", id);
                return new GatewaySubscriptionResult
                {
                    SubscriptionId = id ?? string.Empty,
                    Status = status ?? string.Empty,
                    ClientSecret = approveLink
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during CreateSubscriptionAsync: {Message}", ex.Message);
                return new GatewaySubscriptionResult
                {
                    Status = "failed",
                    ErrorMessage = $"CRITICAL ERROR: {ex.GetType().Name} - {ex.Message} - {ex.StackTrace}",
                    ClientSecret = null
                };
            }
        }

        public async Task<GatewayCancelResult> CancelSubscriptionAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var accessToken = await GetAccessTokenAsync(client);

                var requestBody = new
                {
                    reason = "User requested cancellation"
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_baseUrl}/v1/billing/subscriptions/" +
                    $"{gatewaySubscriptionId}/cancel");

                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", accessToken);

                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await client.SendAsync(request, ct);

                // PayPal returns 204 No Content on success
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);

                    // 404 means the subscription doesn't exist on PayPal.
                    // This happens for Pending subscriptions that were 
                    // never approved by the user. Treat as success — 
                    // there's nothing to cancel on PayPal's side.
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning(
                            "PayPal subscription {Id} not found during " +
                            "cancel — treating as already cancelled.",
                            gatewaySubscriptionId);
                        return new GatewayCancelResult { IsSuccess = true };
                    }

                    _logger.LogError("PayPal cancel failed for {Id}: {Error}", gatewaySubscriptionId, error);
                    return new GatewayCancelResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"PayPal cancel error: {error}"
                    };
                }

                _logger.LogInformation("PayPal subscription cancelled: {Id}", gatewaySubscriptionId);

                return new GatewayCancelResult { IsSuccess = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayPal CancelSubscriptionAsync failed for {Id}", gatewaySubscriptionId);
                return new GatewayCancelResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<GatewayCancelResult> CancelAtPeriodEndAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            // PayPal does not support cancel_at_period_end.
            // Falling back to immediate cancellation.
            _logger.LogWarning(
                "PayPal does not support cancel_at_period_end. " +
                "Cancelling immediately for subscription {Id}",
                gatewaySubscriptionId);

            return await CancelSubscriptionAsync(gatewaySubscriptionId, idempotencyKey, ct);
        }

        public Task<Result<string>> CreateOrGetCustomerAsync(string userId, string email, CancellationToken ct)
        {
            return Task.FromResult(Result.Success(userId));
        }

        public async Task<WebhookParseResult> ParseAndVerifyWebhookAsync(
            string payload, IHeaderDictionary headers, CancellationToken ct)
        {
            if (!headers.TryGetValue("paypal-transmission-id", out var transmissionId) ||
                !headers.TryGetValue("paypal-transmission-time", out var transmissionTime) ||
                !headers.TryGetValue("paypal-cert-url", out var certUrl) ||
                !headers.TryGetValue("paypal-auth-algo", out var authAlgo) ||
                !headers.TryGetValue("paypal-transmission-sig", out var transmissionSig))
            {
                return new WebhookParseResult { IsValid = false };
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var accessToken = await GetAccessTokenAsync(client);

                var requestBody = new
                {
                    auth_algo = authAlgo.ToString(),
                    cert_url = certUrl.ToString(),
                    transmission_id = transmissionId.ToString(),
                    transmission_sig = transmissionSig.ToString(),
                    transmission_time = transmissionTime.ToString(),
                    webhook_id = _options.WebhookId,
                    webhook_event = JsonDocument.Parse(payload).RootElement
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/notifications/verify-webhook-signature");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                using var verifyDoc = JsonDocument.Parse(content);
                var verificationStatus = verifyDoc.RootElement.GetProperty("verification_status").GetString();

                if (verificationStatus != "SUCCESS")
                {
                    _logger.LogWarning("PayPal webhook signature verification failed. Status: {Status}", verificationStatus);
                    return new WebhookParseResult { IsValid = false };
                }

                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var eventType = root.GetProperty("event_type").GetString();

                var result = new WebhookParseResult
                {
                    IsValid = true,
                    EventType = eventType
                };

                if (root.TryGetProperty("resource", out var resource))
                {
                    if (resource.TryGetProperty("id", out var idProp))
                        result.SubscriptionId = idProp.GetString();

                    if (resource.TryGetProperty("custom_id", out var customIdProp))
                        result.CustomerId = customIdProp.GetString();

                    if (resource.TryGetProperty("status", out var statusProp))
                        result.Status = statusProp.GetString();

                    if (eventType == "PAYMENT.SALE.COMPLETED")
                    {
                        if (resource.TryGetProperty("id", out var saleIdProp))
                            result.PaymentId = saleIdProp.GetString();

                        if (resource.TryGetProperty("amount", out var amountProp) &&
                            amountProp.TryGetProperty("total", out var totalProp))
                        {
                            if (decimal.TryParse(totalProp.GetString(), out var amount))
                            {
                                result.Amount = amount;
                            }
                        }
                    }
                    else if (eventType == "BILLING.SUBSCRIPTION.ACTIVATED")
                    {
                        result.PaymentId = null;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayPal webhook: {Message}", ex.Message);
                return new WebhookParseResult { IsValid = false };
            }
        }

    }

    public class PayPalSubscriptionResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<PayPalLink>? Links { get; set; }
    }

    public class PayPalLink
    {
        public string Href { get; set; } = string.Empty;
        public string Rel { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
    }
}
