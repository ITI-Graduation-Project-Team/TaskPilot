using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayPalCheckoutSdk.Core;
//using PaypalServerSdk;
using System.Text.Json;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Gateways;
using TaskPilot.Services.Interfaces.Payments;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Infrastructure.Payments.Gateways
{
    public class PayPalGateway : IPaymentGateway
    {
        private readonly PayPalOptions _options;
        private readonly PayPalHttpClient _client;
        private readonly ILogger<PayPalGateway> _logger;

        public PayPalGateway(IOptions<PayPalOptions> options, ILogger<PayPalGateway> logger)
        {
            _options = options.Value;
            _logger = logger;
            var environment = _options.Mode.Equals("live", StringComparison.OrdinalIgnoreCase)
                ? (PayPalEnvironment)new LiveEnvironment(_options.ClientId, _options.ClientSecret)
                : new SandboxEnvironment(_options.ClientId, _options.ClientSecret);
            _client = new PayPalHttpClient(environment);
        }

        public string ProviderName => "PayPal";
        public TaskPilot.Models.Enums.PaymentGateway GatewayType => TaskPilot.Models.Enums.PaymentGateway.PayPal;

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
            var request = new PayPalHttp.HttpRequest("/v1/billing/subscriptions", HttpMethod.Post, typeof(object));
            request.Headers.Add("Prefer", "return=representation");
            request.Headers.Add("PayPal-Request-Id", idempotencyKey);

            request.Body = new
            {
                plan_id = mappedPlanId,
                subscriber = new
                {
                    custom_id = customerId
                },
                application_context = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl
                }
            };

            try
            {
                var response = await _client.Execute(request);
                var json = JsonSerializer.Serialize(response.Result<object>());
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;
                var id = root.GetProperty("id").GetString();
                var status = root.GetProperty("status").GetString();

                string? approveLink = null;
                if (root.TryGetProperty("links", out var links))
                {
                    foreach (var link in links.EnumerateArray())
                    {
                        if (link.GetProperty("rel").GetString() == "approve")
                        {
                            approveLink = link.GetProperty("href").GetString();
                            break;
                        }
                    }
                }

                _logger.LogInformation("Successfully created PayPal subscription {GatewaySubscriptionId}", id);
                return new GatewaySubscriptionResult
                {
                    SubscriptionId = id ?? string.Empty,
                    Status = status ?? string.Empty,
                    ClientSecret = approveLink
                };
            }
            catch (PayPalHttp.HttpException ex)
            {
                _logger.LogError(ex, "PayPal API error during CreateSubscriptionAsync: {Message}", ex.Message);
                return new GatewaySubscriptionResult
                {
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    ClientSecret = null
                };
            }
        }

        public async Task<GatewayCancelResult> CancelSubscriptionAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            var request = new PayPalHttp.HttpRequest($"/v1/billing/subscriptions/{gatewaySubscriptionId}/cancel", HttpMethod.Post, typeof(object));
            request.Headers.Add("PayPal-Request-Id", idempotencyKey);
            request.Body = new { reason = "User-requested cancellation" };

            try
            {
                await _client.Execute(request);
                return new GatewayCancelResult { IsSuccess = true };
            }
            catch (PayPalHttp.HttpException ex)
            {
                _logger.LogError(ex, "PayPal API error during CancelSubscriptionAsync: {Message}", ex.Message);
                return new GatewayCancelResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public Task<Result<string>> CreateOrGetCustomerAsync(string userId, string email, CancellationToken ct)
        {
            // PayPal doesn't require pre-creating a customer object like Stripe.
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
                var request = new PayPalHttp.HttpRequest("/v1/notifications/verify-webhook-signature", HttpMethod.Post, typeof(object));
                request.Body = new
                {
                    auth_algo = authAlgo.ToString(),
                    cert_url = certUrl.ToString(),
                    transmission_id = transmissionId.ToString(),
                    transmission_sig = transmissionSig.ToString(),
                    transmission_time = transmissionTime.ToString(),
                    webhook_id = _options.WebhookId,
                    webhook_event = JsonSerializer.Deserialize<object>(payload)
                };

                var response = await _client.Execute(request);
                var json = JsonSerializer.Serialize(response.Result<object>());
                using var verifyDoc = JsonDocument.Parse(json);
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

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayPal webhook: {Message}", ex.Message);
                return new WebhookParseResult { IsValid = false };
            }
        }
    }
}
