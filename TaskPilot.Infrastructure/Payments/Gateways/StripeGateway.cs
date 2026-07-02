using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Gateways;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Infrastructure.Payments.Gateways
{
    public class StripeGateway : IPaymentGateway
    {
        private readonly StripeOptions _options;
        private readonly ILogger<StripeGateway> _logger;
        private readonly StripeClient _stripeClient;

        public StripeGateway(IOptions<StripeOptions> options, ILogger<StripeGateway> logger)
        {
            _options = options.Value;
            _logger = logger;
            _stripeClient = new StripeClient(_options.SecretKey);
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
                var service = new SubscriptionService(_stripeClient);
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
                var service = new SubscriptionService(_stripeClient);
                var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
                await service.CancelAsync(gatewaySubscriptionId, null, requestOptions, ct);

                return new GatewayCancelResult { IsSuccess = true };
            }
            catch (StripeException ex)
            {
                return new GatewayCancelResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<GatewayCancelResult> CancelAtPeriodEndAsync(
            string gatewaySubscriptionId, string idempotencyKey, CancellationToken ct)
        {
            try
            {
                var service = new SubscriptionService(_stripeClient);
                var options = new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                };

                var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
                await service.UpdateAsync(gatewaySubscriptionId, options, requestOptions, ct);

                return new GatewayCancelResult { IsSuccess = true };
            }
            catch (StripeException ex)
            {
                return new GatewayCancelResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<Result<string>> CreateOrGetCustomerAsync(string userId, string email, CancellationToken ct)
        {
            try
            {
                var service = new CustomerService(_stripeClient);

                // Search for existing
                var searchOptions = new CustomerSearchOptions
                {
                    Query = $"metadata['user_id']:'{userId}'"
                };
                var existing = await service.SearchAsync(searchOptions, null, ct);

                if (existing.Data.Count > 0)
                {
                    return Result.Success(existing.Data[0].Id);
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
                return Result.Success(customer.Id);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe API error during CreateOrGetCustomerAsync: {Message}", ex.StripeError?.Message ?? ex.Message);
                return Result.Failure<string>(new Error("GatewayError", ErrorType.Failure, ex.StripeError?.Message ?? ex.Message));
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
        public async Task<GatewaySubscriptionResult> CreateTrialSubscriptionAsync(
            string customerId, string priceId, int trialDays,
            string paymentMethodId, string idempotencyKey,
            CancellationToken ct)
        {
            try
            {
                var options = new SubscriptionCreateOptions
                {
                    Customer = customerId,
                    Items = new System.Collections.Generic.List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions { Price = priceId }
                    },
                    TrialPeriodDays = trialDays,
                    PaymentBehavior = "default_incomplete",
                    PaymentSettings = new SubscriptionPaymentSettingsOptions
                    {
                        SaveDefaultPaymentMethod = "on_subscription"
                    },
                    Expand = new System.Collections.Generic.List<string>
                    {
                        "latest_invoice.payment_intent",
                        "pending_setup_intent"
                    }
                };

                var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
                var service = new SubscriptionService(_stripeClient);
                var subscription = await service.CreateAsync(options, requestOptions, ct);

                // Trial subscriptions return a SetupIntent, not PaymentIntent
                var setupIntentClientSecret = subscription.PendingSetupIntent?.ClientSecret;

                return new GatewaySubscriptionResult
                {
                    SubscriptionId = subscription.Id,
                    ClientSecret = setupIntentClientSecret,
                    IsSetupIntent = true,
                    Status = subscription.Status
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe trial creation failed: {Message}", ex.StripeError?.Message ?? ex.Message);
                return new GatewaySubscriptionResult
                {
                    Status = "Failed",
                    ErrorMessage = ex.StripeError?.Message ?? ex.Message,
                    ClientSecret = null
                };
            }
        }
    }
}
