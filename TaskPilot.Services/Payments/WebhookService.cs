using Microsoft.AspNetCore.Http;
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
        private readonly IUnitOfWork _unitOfWork;

        public WebhookService(
            IPaymentGatewayFactory gatewayFactory,
            IRepository<UserSubscription> subRepo,
            IRepository<Payment> paymentRepo,
            IUnitOfWork unitOfWork)
        {
            _gatewayFactory = gatewayFactory;
            _subRepo = subRepo;
            _paymentRepo = paymentRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> HandleWebhookAsync(string gatewayName, string payload, IHeaderDictionary headers)
        {
            if (!Enum.TryParse<PaymentGateway>(gatewayName, true, out var gatewayType))
                return Result.Failure(CommonErrors.InvalidInput("Invalid Gateway"));

            var gateway = _gatewayFactory.GetGateway(gatewayType);
            var result = await gateway.ParseAndVerifyWebhookAsync(payload, headers, default);
            
            if (!result.IsValid)
                return Result.Failure(CommonErrors.InvalidInput("Invalid Signature"));

            // For simplicity, just handling successful payment and cancel events
            if (!string.IsNullOrEmpty(result.SubscriptionId))
            {
                var sub = await _subRepo.FindSingleAsync(s => s.GatewaySubscriptionId == result.SubscriptionId);
                if (sub != null)
                {
                    if (result.EventType == "customer.subscription.deleted" || result.EventType == "BILLING.SUBSCRIPTION.CANCELLED")
                    {
                        sub.Status = SubscriptionStatus.Canceled;
                        sub.CanceledAt = DateTime.UtcNow;
                        _subRepo.Update(sub);
                    }
                    else if (result.EventType == "invoice.payment_failed" || result.EventType == "PAYMENT.SALE.DENIED")
                    {
                        sub.Status = SubscriptionStatus.Canceled;
                        sub.CanceledAt = DateTime.UtcNow;
                        _subRepo.Update(sub);
                    }
                    else if (result.EventType == "invoice.payment_succeeded" || result.EventType == "PAYMENT.SALE.COMPLETED")
                    {
                        if (!string.IsNullOrEmpty(result.PaymentId))
                        {
                            var existingPayment = await _paymentRepo.FindSingleAsync(p => p.GatewayTransactionId == result.PaymentId);
                            if (existingPayment != null)
                                return Result.Success(); // Idempotency check: already processed
                        }

                        sub.Status = SubscriptionStatus.Active;
                        _subRepo.Update(sub);
                        
                        var payment = new Payment
                        {
                            ProjectManagerId = sub.ProjectManagerId,
                            UserSubscriptionId = sub.Id,
                            GatewayTransactionId = result.PaymentId,
                            Amount = result.Amount,
                            Currency = result.Currency,
                            Status = PaymentStatus.Completed,
                            PaymentGateway = gatewayType,
                            PaymentMethod = gatewayType == PaymentGateway.Stripe ? PaymentMethod.CreditCard : PaymentMethod.Wallet,
                            PaidAt = DateTime.UtcNow
                        };
                        await _paymentRepo.AddAsync(payment);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(default);

            return Result.Success();
        }
    }
}
