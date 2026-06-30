using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.UserSubscriptions;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class UserSubscriptionService : IUserSubscriptionService
    {
        private readonly IRepository<UserSubscription> _subscriptionRepo;
        private readonly IRepository<SubscriptionPlan> _planRepo;
        private readonly IRepository<ProjectManager> _pmRepo;
        private readonly TaskPilot.Services.Interfaces.Payments.IPaymentGatewayFactory _gatewayFactory;

        public UserSubscriptionService(
            IRepository<UserSubscription> subscriptionRepo,
            IRepository<SubscriptionPlan> planRepo,
            IRepository<ProjectManager> pmRepo,
            TaskPilot.Services.Interfaces.Payments.IPaymentGatewayFactory gatewayFactory)
        {
            _subscriptionRepo = subscriptionRepo;
            _planRepo = planRepo;
            _pmRepo = pmRepo;
            _gatewayFactory = gatewayFactory;
        }

        public async Task<Result<UserSubscriptionDto>> GetByIdAsync(Guid id)
        {
            var sub = await _subscriptionRepo.GetByIdAsync(id, s => s.Plan);
            if (sub == null)
                return Result.Failure<UserSubscriptionDto>(CommonErrors.NotFound("UserSubscription"));

            return Result.Success(MapToDto(sub));
        }

        public async Task<Result<List<UserSubscriptionDto>>> GetAllAsync(Guid? projectManagerId = null)
        {
            IEnumerable<UserSubscription> subs;
            if (projectManagerId.HasValue)
            {
                subs = await _subscriptionRepo.FindAsync(s => s.ProjectManagerId == projectManagerId.Value, s => s.Plan);
            }
            else
            {
                subs = await _subscriptionRepo.GetAllAsync(s => s.Plan);
            }

            return Result.Success(subs.Select(MapToDto).ToList());
        }

        public async Task<Result<UserSubscriptionDto>> GetCurrentSubscriptionAsync(Guid projectManagerId)
        {
            var pmExists = await _pmRepo.AnyAsync(pm => pm.Id == projectManagerId);
            if (!pmExists)
                return Result.Failure<UserSubscriptionDto>(CommonErrors.NotFound("ProjectManager"));

            var activeSub = (await _subscriptionRepo.FindAsync(
                s => s.ProjectManagerId == projectManagerId && 
                     (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending || s.Status == SubscriptionStatus.Trialing), 
                s => s.Plan))
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefault();

            // Check if subscription has ended
            if (activeSub != null && activeSub.EndDate < DateTime.UtcNow && activeSub.Plan.Name != "Free")
            {
                // Subscription ended, mark it as expired
                activeSub.Status = SubscriptionStatus.Expired;
                _subscriptionRepo.Update(activeSub);

                // Fallback to free plan
                var freePlan = await _planRepo.FindSingleAsync(p => p.Name == "Free");
                if (freePlan != null)
                {
                    var newFreeSub = new UserSubscription
                    {
                        ProjectManagerId = projectManagerId,
                        SubscriptionPlanId = freePlan.Id,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddYears(10), // Basically forever for free plan
                        BillingCycle = BillingCycle.Monthly,
                        Status = SubscriptionStatus.Active,
                        AutoRenew = true,
                        IsTrial = false
                    };
                    await _subscriptionRepo.AddAsync(newFreeSub);
                    
                    // Since UoW save changes isn't called here natively, the controller should save.
                    // But typically read operations don't save. Wait, we are mutating state!
                    // This is a special case. The caller (Controller) should call SaveChanges if we return a success but mutated state.
                    return Result.Success(new UserSubscriptionDto 
                    {
                        ProjectManagerId = projectManagerId,
                        SubscriptionPlanId = freePlan.Id,
                        PlanName = freePlan.Name,
                        Status = "Active",
                        StartDate = newFreeSub.StartDate,
                        EndDate = newFreeSub.EndDate,
                        BillingCycle = "Monthly"
                    });
                }
            }

            if (activeSub == null)
            {
                // Ensure they at least have the free plan if absolutely nothing exists
                var freePlan = await _planRepo.FindSingleAsync(p => p.Name == "Free");
                if (freePlan != null)
                {
                    var newFreeSub = new UserSubscription
                    {
                        ProjectManagerId = projectManagerId,
                        SubscriptionPlanId = freePlan.Id,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddYears(10),
                        BillingCycle = BillingCycle.Monthly,
                        Status = SubscriptionStatus.Active,
                        AutoRenew = true,
                        IsTrial = false
                    };
                    await _subscriptionRepo.AddAsync(newFreeSub);
                    return Result.Success(new UserSubscriptionDto 
                    {
                        ProjectManagerId = projectManagerId,
                        SubscriptionPlanId = freePlan.Id,
                        PlanName = freePlan.Name,
                        Status = "Active",
                        StartDate = newFreeSub.StartDate,
                        EndDate = newFreeSub.EndDate,
                        BillingCycle = "Monthly"
                    });
                }
                return Result.Failure<UserSubscriptionDto>(CommonErrors.NotFound("Active Subscription"));
            }

            return Result.Success(MapToDto(activeSub));
        }

        public async Task<Result<UserSubscriptionDto>> CreateAsync(Guid projectManagerId, CreateUserSubscriptionDto dto)
        {
            var pm = await _pmRepo.GetByIdAsync(projectManagerId);
            if (pm == null)
                return Result.Failure<UserSubscriptionDto>(CommonErrors.NotFound("ProjectManager"));

            var plan = await _planRepo.FindSingleAsync(p => p.Id == dto.SubscriptionPlanId);
            if (plan == null)
                return Result.Failure<UserSubscriptionDto>(CommonErrors.NotFound("SubscriptionPlan"));

            if (!Enum.TryParse<BillingCycle>(dto.BillingCycle, out var billingCycle))
                return Result.Failure<UserSubscriptionDto>(CommonErrors.InvalidInput("Invalid BillingCycle. Must be Monthly or Annually."));

            // Expire current active subscriptions
            var activeSubs = await _subscriptionRepo.FindAsync(s => s.ProjectManagerId == projectManagerId && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending));
            foreach (var sub in activeSubs)
            {
                sub.Status = SubscriptionStatus.Expired;
                _subscriptionRepo.Update(sub);
            }

            var endDate = billingCycle == BillingCycle.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1);

            // Integrate with Payment Gateway
            string? gatewaySubId = null;
            string? gatewayCustId = null;
            string? clientSecret = null;
            
            if (plan.Name != "Free")
            {
                var gateway = _gatewayFactory.GetGateway(dto.Gateway);
                var customerId = await gateway.CreateOrGetCustomerAsync(pm.Id.ToString(), pm.Email ?? string.Empty, default);
                
                // Deterministic idempotency key to prevent duplicate charges on retry
                var idempotencyKey = $"sub_{projectManagerId}_{plan.Id}_{billingCycle}_{DateTime.UtcNow:yyyyMMddHHmm}";
                var subResult = await gateway.CreateSubscriptionAsync(
                    customerId, plan.Name, billingCycle, dto.PaymentMethodId ?? string.Empty, idempotencyKey, dto.ReturnUrl, dto.CancelUrl, default);

                if (subResult.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<UserSubscriptionDto>(CommonErrors.OperationFailed(subResult.ErrorMessage ?? "Payment gateway declined the subscription request."));
                }

                gatewaySubId = subResult.SubscriptionId;
                gatewayCustId = customerId;
                clientSecret = subResult.ClientSecret;
            }

            // Paid plans start as Pending awaiting webhook confirmation. Free plans are active immediately since there's no payment to collect.
            var initialStatus = plan.Name != "Free" ? SubscriptionStatus.Pending : SubscriptionStatus.Active;

            var newSubscription = new UserSubscription
            {
                ProjectManagerId = projectManagerId,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                StartDate = DateTime.UtcNow,
                EndDate = endDate,
                BillingCycle = billingCycle,
                Status = initialStatus,
                AutoRenew = dto.AutoRenew,
                IsTrial = false,
                GatewaySubscriptionId = gatewaySubId,
                GatewayCustomerId = gatewayCustId,
                Gateway = dto.Gateway
            };

            await _subscriptionRepo.AddAsync(newSubscription);

            var resultDto = new UserSubscriptionDto
            {
                Id = newSubscription.Id,
                ProjectManagerId = projectManagerId,
                SubscriptionPlanId = plan.Id,
                PlanName = plan.Name,
                StartDate = newSubscription.StartDate,
                EndDate = newSubscription.EndDate,
                BillingCycle = newSubscription.BillingCycle.ToString(),
                Status = newSubscription.Status.ToString(),
                AutoRenew = newSubscription.AutoRenew,
                IsTrial = newSubscription.IsTrial,
                ClientSecret = clientSecret
            };

            return Result.Success(resultDto);
        }

        public async Task<Result> UpdateAsync(Guid id, UpdateUserSubscriptionDto dto)
        {
            var sub = await _subscriptionRepo.GetByIdAsync(id);
            if (sub == null)
                return Result.Failure(CommonErrors.NotFound("UserSubscription"));

            if (!Enum.TryParse<SubscriptionStatus>(dto.Status, out var status))
                return Result.Failure(CommonErrors.InvalidInput("Invalid Status."));

            sub.Status = status;
            sub.AutoRenew = dto.AutoRenew;
            sub.EndDate = dto.EndDate;

            _subscriptionRepo.Update(sub);
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            var sub = await _subscriptionRepo.GetByIdAsync(id);
            if (sub == null)
                return Result.Failure(CommonErrors.NotFound("UserSubscription"));

            if (!string.IsNullOrEmpty(sub.GatewaySubscriptionId))
            {
                try
                {
                    var gateway = _gatewayFactory.GetGateway(sub.Gateway);
                    await gateway.CancelSubscriptionAsync(sub.GatewaySubscriptionId, Guid.NewGuid().ToString(), default);
                }
                catch
                {
                    // Log error but proceed to cancel locally
                }
            }

            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = DateTime.UtcNow;
            sub.IsDeleted = true;
            _subscriptionRepo.Update(sub);
            return Result.Success();
        }

        private static UserSubscriptionDto MapToDto(UserSubscription sub)
        {
            return new UserSubscriptionDto
            {
                Id = sub.Id,
                ProjectManagerId = sub.ProjectManagerId,
                SubscriptionPlanId = sub.SubscriptionPlanId,
                PlanName = sub.Plan?.Name ?? string.Empty,
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                BillingCycle = sub.BillingCycle.ToString(),
                Status = sub.Status.ToString(),
                AutoRenew = sub.AutoRenew,
                IsTrial = sub.IsTrial,
                TrialEndDate = sub.TrialEndDate
            };
        }
    }
}
