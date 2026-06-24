using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PayPalCheckoutSdk.Core;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Models.Gateways;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Infrastructure.Payments.Gateways
{
    public class PayPalGateway : IPaymentGateway
    {
        private readonly PayPalOptions _options;
        private readonly PayPalHttpClient _client;

        public PayPalGateway(IOptions<PayPalOptions> options)
        {
            _options = options.Value;
            var environment = _options.Mode.Equals("live", StringComparison.OrdinalIgnoreCase)
                ? (PayPalEnvironment)new LiveEnvironment(_options.ClientId, _options.ClientSecret)
                : new SandboxEnvironment(_options.ClientId, _options.ClientSecret);
            _client = new PayPalHttpClient(environment);
        }

        public string ProviderName => "PayPal";
        public TaskPilot.Models.Enums.PaymentGateway GatewayType => TaskPilot.Models.Enums.PaymentGateway.PayPal;

        public async Task<GatewaySubscriptionResult> CreateSubscriptionAsync(
            string customerId, string planId, BillingCycle interval, 
            string paymentMethodId, string idempotencyKey, CancellationToken ct)
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

            var request = new PayPalHttp.HttpRequest("/v1/billing/subscriptions", HttpMethod.Post, typeof(object));
            request.Headers.Add("Prefer", "return=representation");
            request.Headers.Add("PayPal-Request-Id", idempotencyKey);
            
            request.Body = new
            {
                plan_id = mappedPlanId,
                subscriber = new
                {
                    custom_id = customerId
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

                return new GatewaySubscriptionResult
                {
                    SubscriptionId = id ?? string.Empty,
                    Status = status ?? string.Empty,
                    ClientSecret = approveLink 
                };
            }
            catch (PayPalHttp.HttpException ex)
            {
                return new GatewaySubscriptionResult
                {
                    Status = "failed",
                    ClientSecret = ex.Message
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
                return new GatewayCancelResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public Task<string> CreateOrGetCustomerAsync(string userId, string email, CancellationToken ct)
        {
            // PayPal doesn't require pre-creating a customer object like Stripe.
            return Task.FromResult(userId);
        }

        public async Task<WebhookParseResult> ParseAndVerifyWebhookAsync(
            string payload, IHeaderDictionary headers, CancellationToken ct)
        {
            // Simplified verification for demo purposes
            if (!headers.TryGetValue("paypal-transmission-id", out _))
            {
                return new WebhookParseResult { IsValid = false };
            }

            try
            {
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
                        if (resource.TryGetProperty("amount", out var amountProp) &&
                            amountProp.TryGetProperty("total", out var totalProp))
                        {
                            if (decimal.TryParse(totalProp.GetString(), out var amount))
                            {
                                result.Amount = amount;
                            }
                        }
                    }
                }

                return await Task.FromResult(result);
            }
            catch
            {
                return new WebhookParseResult { IsValid = false };
            }
        }
    }
}
