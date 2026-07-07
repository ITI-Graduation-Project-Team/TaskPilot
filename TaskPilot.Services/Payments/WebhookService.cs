using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces.Payments;

using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Services.Payments
{
    public class WebhookService : IWebhookService
    {
        private readonly IPaymentGatewayFactory _gatewayFactory;
        private readonly IRepository<UserSubscription> _subRepo;
        private readonly IRepository<Payment> _paymentRepo;
        private readonly IRepository<SubscriptionPlan> _planRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WebhookService> _logger;

        public WebhookService(
            IPaymentGatewayFactory gatewayFactory,
            IRepository<UserSubscription> subRepo,
            IRepository<Payment> paymentRepo,
            IRepository<SubscriptionPlan> planRepo,
            IUnitOfWork unitOfWork,
            ILogger<WebhookService> logger)
        {
            _gatewayFactory = gatewayFactory;
            _subRepo = subRepo;
            _paymentRepo = paymentRepo;
            _planRepo = planRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> HandleWebhookAsync(string gatewayName, string payload, IHeaderDictionary headers)
        {
            if (!Enum.TryParse<PaymentGateway>(gatewayName, true, out var gatewayType))
                return Result.Failure(CommonErrors.InvalidInput("Invalid Gateway"));

            var gateway = _gatewayFactory.GetGateway(gatewayType);
            var result = await gateway.ParseAndVerifyWebhookAsync(payload, headers, default);
            
            if (!result.IsValid)
            {
                _logger.LogWarning("Invalid webhook signature from gateway {Gateway}", gatewayName);
                return Result.Failure(CommonErrors.InvalidInput("Invalid Signature"));
            }

            try
            {
                _logger.LogInformation("Webhook received: {EventType} from {Gateway}", result.EventType, gatewayName);

                if (!string.IsNullOrEmpty(result.SubscriptionId))
                {
                    var sub = await _subRepo.FindSingleAsync(s => s.GatewaySubscriptionId == result.SubscriptionId);
                    if (sub != null)
                    {
                        if (result.EventType == "BILLING.SUBSCRIPTION.CANCELLED")
                        {
                            sub.Status = SubscriptionStatus.Expired;
                            sub.CanceledAt = DateTime.UtcNow;
                            _subRepo.Update(sub);

                            var freePlan = await _planRepo.FindSingleAsync(p => p.MonthlyPrice == 0 && p.AnnualPrice == 0);
                            if (freePlan == null)
                            {
                                _logger.LogError("Free plan not found when transitioning subscription {SubscriptionId}", sub.Id);
                                return Result.Failure(CommonErrors.ServerError("Free plan not found"));
                            }

                            var freeSub = new UserSubscription
                            {
                                ProjectManagerId = sub.ProjectManagerId,
                                SubscriptionPlanId = freePlan.Id,
                                Status = SubscriptionStatus.Active,
                                StartDate = DateTime.UtcNow,
                                EndDate = DateTime.UtcNow.AddYears(100),
                                BillingCycle = BillingCycle.Monthly,
                                AutoRenew = false,
                                GatewaySubscriptionId = null,
                                GatewayCustomerId = null,
                                CancelAtPeriodEnd = false
                            };
                            await _subRepo.AddAsync(freeSub);

                            _logger.LogInformation("Subscription {Id} expired, user {UserId} moved to Free plan", sub.Id, sub.ProjectManagerId);
                        }
                        else if (result.EventType == "PAYMENT.SALE.DENIED")
                        {
                            sub.Status = SubscriptionStatus.Canceled;
                            sub.CanceledAt = DateTime.UtcNow;
                            _subRepo.Update(sub);
                            _logger.LogInformation("Subscription {SubscriptionId} transitioned to {NewStatus} via {Gateway} webhook event {EventType}", sub.Id, sub.Status, gatewayName, result.EventType);
                        }
                        else if (result.EventType == "PAYMENT.SALE.COMPLETED" || result.EventType == "BILLING.SUBSCRIPTION.ACTIVATED")
                        {
                            if (result.EventType == "BILLING.SUBSCRIPTION.ACTIVATED" || result.EventType == "PAYMENT.SALE.COMPLETED")
                            {
                                // Status-based idempotency: if already Active, skip
                                if (sub.Status == SubscriptionStatus.Active)
                                {
                                    _logger.LogInformation(
                                        "Duplicate {EventType} webhook ignored — subscription {Id} already Active",
                                        result.EventType, sub.Id);
                                    return Result.Success();
                                }
                            }

                            if (!string.IsNullOrEmpty(result.PaymentId))
                            {
                                var existingPayment = await _paymentRepo.FindSingleAsync(p => p.GatewayTransactionId == result.PaymentId);
                                if (existingPayment != null)
                                    return Result.Success(); // Idempotency check: already processed
                            }

                            sub.Status = SubscriptionStatus.Active;
                            _subRepo.Update(sub);
                            _logger.LogInformation("Subscription {SubscriptionId} transitioned to {NewStatus} via {Gateway} webhook event {EventType}", sub.Id, sub.Status, gatewayName, result.EventType);
                            
                            var payment = new Payment
                            {
                                ProjectManagerId = sub.ProjectManagerId,
                                UserSubscriptionId = sub.Id,
                                GatewayTransactionId = result.PaymentId,
                                Amount = result.Amount,
                                Currency = result.Currency,
                                Status = PaymentStatus.Completed,
                                PaymentGateway = gatewayType,
                                PaymentMethod = gatewayType == PaymentGateway.Paymob ? PaymentMethod.CreditCard : PaymentMethod.Wallet,
                                PaidAt = DateTime.UtcNow
                            };
                            await _paymentRepo.AddAsync(payment);
                        }
                        else if (result.EventType == "transaction.success")
                        {
                            // Status-based idempotency
                            if (sub.Status == SubscriptionStatus.Active)
                            {
                                _logger.LogInformation(
                                    "Duplicate transaction.success webhook " +
                                    "ignored — subscription {Id} already Active",
                                    sub.Id);
                                return Result.Success();
                            }

                            // Transaction ID idempotency check
                            if (!string.IsNullOrEmpty(result.PaymentId))
                            {
                                var existingPayment = await _paymentRepo
                                    .FindSingleAsync(p => 
                                        p.GatewayTransactionId == result.PaymentId);
                                if (existingPayment != null)
                                    return Result.Success();
                            }

                            sub.Status = SubscriptionStatus.Active;
                            _subRepo.Update(sub);
                            _logger.LogInformation(
                                "Paymob payment success for order {Id}", 
                                result.SubscriptionId);

                            var payment = new Payment
                            {
                                ProjectManagerId = sub.ProjectManagerId,
                                UserSubscriptionId = sub.Id,
                                GatewayTransactionId = result.PaymentId,
                                Amount = result.Amount,
                                Currency = result.Currency,
                                Status = PaymentStatus.Completed,
                                PaymentGateway = gatewayType,
                                PaymentMethod = PaymentMethod.CreditCard,
                                PaidAt = DateTime.UtcNow
                            };
                            await _paymentRepo.AddAsync(payment);
                        }
                        else if (result.EventType == "transaction.failed")
                        {
                            sub.Status = SubscriptionStatus.Canceled;
                            sub.CanceledAt = DateTime.UtcNow;
                            _subRepo.Update(sub);
                            _logger.LogInformation(
                                "Paymob payment failed for order {Id}", 
                                result.SubscriptionId);
                        }
                        else
                        {
                            _logger.LogWarning("Unhandled webhook event type: {EventType} from {Gateway} — no action taken", result.EventType, gatewayName);
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync(default);

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing failed for event {EventType}: {ErrorMessage}", result.EventType, ex.Message);
                return Result.Failure(CommonErrors.ServerError("Webhook processing failed"));
            }
        }
    }
}
