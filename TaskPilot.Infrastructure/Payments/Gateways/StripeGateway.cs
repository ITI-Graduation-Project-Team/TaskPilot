using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Models.Gateways;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces.Payments;
using Microsoft.Extensions.Logging;

namespace TaskPilot.Infrastructure.Payments.Gateways
{
    public class StripeGateway : IPaymentGateway
    {
        private readonly StripeOptions _options;
        private readonly ILogger<StripeGateway> _logger;

        public StripeGateway(IOptions<StripeOptions> options, ILogger<StripeGateway> logger)
        {
            _options = options.Value;
            _logger = logger;
            StripeConfiguration.ApiKey = _options.SecretKey;
        }

        public string ProviderName => "Stripe";
        public TaskPilot.Models.Enums.PaymentGateway GatewayType => TaskPilot.Models.Enums.PaymentGateway.Stripe;

        public async Task<GatewaySubscriptionResult> CreateSubscriptionAsync(
            string customerId, string planId, BillingCycle interval, 
            string paymentMethodId, string idempotencyKey, 
            string? returnUrl, string? cancelUrl, CancellationToken ct)
        {
            if (!_options.PlanMappings.TryGetValue(planId, out var mapping))
            {
                return new GatewaySubscriptionResult { Status = "Failed", ErrorMessage = $"No mapping found for plan '{planId}' in Stripe settings." };
            }

            var priceId = interval == BillingCycle.Monthly ? mapping.MonthlyPriceId : mapping.AnnualPriceId;
            if (string.IsNullOrEmpty(priceId))
            {
                return new GatewaySubscriptionResult { Status = "Failed", ErrorMessage = $"No Stripe price ID configured for plan '{planId}' ({interval})." };
            }

            try
            {
                var options = new SubscriptionCreateOptions
                {
                    Customer = customerId,
                    Items = new System.Collections.Generic.List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            Price = priceId 
                        }
                    },
                    PaymentSettings = new SubscriptionPaymentSettingsOptions
                    {
                        PaymentMethodTypes = new System.Collections.Generic.List<string> { "card" },
                        SaveDefaultPaymentMethod = "on_subscription"
                    },
                    Expand = new System.Collections.Generic.List<string> { "latest_invoice.payment_intent" }
                };

                var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
                var service = new SubscriptionService();
                var subscription = await service.CreateAsync(options, requestOptions, ct);

                return new GatewaySubscriptionResult
                {
                    SubscriptionId = subscription.Id,
                    Status = subscription.Status,
                    ClientSecret = subscription.LatestInvoice?.PaymentIntent?.ClientSecret
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe API error during CreateSubscriptionAsync: {Message}", ex.StripeError?.Message ?? ex.Message);
                return new GatewaySubscriptionResult 
                { 
                    Status = "Failed", 
                    ErrorMessage = ex.StripeError?.Message ?? ex.Message 
                };
            }
        }

        public async Task<GatewayCancelResult> CancelSubscriptionAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            try
            {
                var service = new SubscriptionService();
                var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
                await service.CancelAsync(gatewaySubscriptionId, null, requestOptions, ct);
                
                return new GatewayCancelResult { IsSuccess = true };
            }
            catch (StripeException ex)
            {
                return new GatewayCancelResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<string> CreateOrGetCustomerAsync(string userId, string email, CancellationToken ct)
        {
            try
            {
                var service = new CustomerService();
                
                // Search for existing
                var searchOptions = new CustomerSearchOptions
                {
                    Query = $"metadata['user_id']:'{userId}'"
                };
                var existing = await service.SearchAsync(searchOptions, null, ct);
                
                if (existing.Data.Count > 0)
                {
                    return existing.Data[0].Id;
                }

                // Create new
                var createOptions = new CustomerCreateOptions
                {
                    Email = email,
                    Metadata = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "user_id", userId }
                    }
                };
                var customer = await service.CreateAsync(createOptions, null, ct);
                return customer.Id;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe API error during CreateOrGetCustomerAsync: {Message}", ex.StripeError?.Message ?? ex.Message);
                throw; // Rethrowing because the interface requires returning a string (customer ID). We log it at least.
            }
        }

        public async Task<WebhookParseResult> ParseAndVerifyWebhookAsync(
            string payload, IHeaderDictionary headers, CancellationToken ct)
        {
            if (!headers.TryGetValue("Stripe-Signature", out var signature))
            {
                return new WebhookParseResult { IsValid = false };
            }

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    payload, signature, _options.WebhookSecret);

                var result = new WebhookParseResult
                {
                    IsValid = true,
                    EventType = stripeEvent.Type
                };

                if (stripeEvent.Data.Object is Invoice invoice)
                {
                    result.SubscriptionId = invoice.SubscriptionId;
                    result.CustomerId = invoice.CustomerId;
                    result.PaymentId = invoice.PaymentIntentId;
                    result.Amount = invoice.AmountPaid / 100m;
                    result.Currency = invoice.Currency;
                    result.Status = invoice.Status;
                }
                else if (stripeEvent.Data.Object is Subscription sub)
                {
                    result.SubscriptionId = sub.Id;
                    result.CustomerId = sub.CustomerId;
                    result.Status = sub.Status;
                }

                return result;
            }
            catch
            {
                return new WebhookParseResult { IsValid = false };
            }
        }
    }
}
