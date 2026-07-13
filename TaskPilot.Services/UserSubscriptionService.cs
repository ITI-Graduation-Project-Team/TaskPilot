using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.UserSubscriptions;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Gateways;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Services
{
    public class UserSubscriptionService : IUserSubscriptionService
    {
        private readonly IRepository<UserSubscription> _subscriptionRepo;
        private readonly IRepository<SubscriptionPlan> _planRepo;
        private readonly IRepository<ProjectManager> _pmRepo;
        private readonly IPaymentGatewayFactory _gatewayFactory;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserSubscriptionService> _logger;

        public UserSubscriptionService(
            IRepository<UserSubscription> subscriptionRepo,
            IRepository<SubscriptionPlan> planRepo,
            IRepository<ProjectManager> pmRepo,
            IPaymentGatewayFactory gatewayFactory,
            IUnitOfWork unitOfWork,
            ILogger<UserSubscriptionService> logger)
        {
            _subscriptionRepo = subscriptionRepo;
            _planRepo = planRepo;
            _pmRepo = pmRepo;
            _gatewayFactory = gatewayFactory;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<UserSubscriptionDto>> GetByIdAsync(Guid id)
        {
            var sub = await _subscriptionRepo.GetByIdAsync(id, s => s.Plan);
            if (sub == null)
                return Result.Failure<UserSubscriptionDto>(UserSubscriptionErrors.NotFound);

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
                return Result.Failure<UserSubscriptionDto>(UserErrors.ProjectManagerNotFound);

            var activeSub = (await _subscriptionRepo.FindAsync(
                s => s.ProjectManagerId == projectManagerId && 
                     (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending || s.Status == SubscriptionStatus.Trialing), 
                s => s.Plan))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            if (activeSub == null)
            {
                return Result.Failure<UserSubscriptionDto>(UserSubscriptionErrors.ActiveSubscriptionNotFound);
            }

            return Result.Success(MapToDto(activeSub));
        }

        public async Task<Result<UserSubscriptionDto>> CreateAsync(Guid projectManagerId, CreateUserSubscriptionDto dto)
        {
            var pmExists = await _pmRepo.AnyAsync(pm => pm.Id == projectManagerId);
            if (!pmExists)
                return Result.Failure<UserSubscriptionDto>(UserErrors.ProjectManagerNotFound);

            var plan = await _planRepo.FindSingleAsync(p => p.Id == dto.SubscriptionPlanId);
            if (plan == null)
                return Result.Failure<UserSubscriptionDto>(SubscriptionPlanErrors.NotFound);

            if (!Enum.TryParse<BillingCycle>(dto.BillingCycle, out var billingCycle))
                return Result.Failure<UserSubscriptionDto>(UserSubscriptionErrors.InvalidBillingCycle);

            // Expire current pending subscriptions only (leave active untouched until confirmed)
            var activeSubs = await _subscriptionRepo.FindAsync(s => s.ProjectManagerId == projectManagerId && s.Status == SubscriptionStatus.Pending);
            foreach (var sub in activeSubs)
            {
                sub.Status = SubscriptionStatus.Expired;
                _subscriptionRepo.Update(sub);
            }

            var isFree = plan.MonthlyPrice == 0 && plan.AnnualPrice == 0;

            if (isFree)
            {
                var freeSubscription = new UserSubscription
                {
                    ProjectManagerId = projectManagerId,
                    SubscriptionPlanId = dto.SubscriptionPlanId,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddYears(100),
                    BillingCycle = billingCycle,
                    Status = SubscriptionStatus.Active,
                    AutoRenew = false,
                    IsTrial = false,
                    Gateway = default(PaymentGateway),
                    GatewaySubscriptionId = null,
                    GatewayCustomerId = null
                };

                await _subscriptionRepo.AddAsync(freeSubscription);
                await _unitOfWork.SaveChangesAsync();

                var freeDto = MapToDto(freeSubscription);
                freeDto.ClientSecret = null;
                return Result.Success(freeDto);
            }

            if (!isFree && dto.Gateway == null)
                return Result.Failure<UserSubscriptionDto>(new Error("ValidationError", ErrorType.Validation, "Gateway is required for paid plans."));

            var gateway = _gatewayFactory.GetGateway(dto.Gateway.Value);
            var pm = await _pmRepo.GetByIdAsync(projectManagerId);
            var customerResult = await gateway.CreateOrGetCustomerAsync(projectManagerId.ToString(), pm?.Email ?? "pm@example.com", default);
            
            if (!customerResult.IsSuccess)
                return Result.Failure<UserSubscriptionDto>(customerResult.Error);

            var customerId = customerResult.Value;

            var idempotencyKey = Guid.NewGuid().ToString();

            var gatewayResult = await gateway.CreateSubscriptionAsync(
                customerId,
                plan.Id.ToString(),
                billingCycle,
                dto.PaymentMethodId ?? "",
                idempotencyKey,
                dto.ReturnUrl,
                dto.CancelUrl,
                default);

            if (!string.IsNullOrEmpty(gatewayResult.ErrorMessage))
                return Result.Failure<UserSubscriptionDto>(new Error("GatewayError", ErrorType.Failure, gatewayResult.ErrorMessage));

            var endDate = billingCycle == BillingCycle.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1);

            var newSubscription = new UserSubscription
            {
                ProjectManagerId = projectManagerId,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                StartDate = DateTime.UtcNow,
                EndDate = endDate,
                BillingCycle = billingCycle,
                Status = SubscriptionStatus.Pending,
                AutoRenew = dto.AutoRenew,
                Gateway = dto.Gateway.Value,
                GatewaySubscriptionId = gatewayResult.SubscriptionId,
                GatewayCustomerId = customerId,
                ClientSecret = gatewayResult.ClientSecret
            };

            await _subscriptionRepo.AddAsync(newSubscription);
            
            try
            {
                await _unitOfWork.SaveChangesAsync(); // Ensure Id is populated
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_UserSubscriptions_PM_Pending") == true)
            {
                var existingPending = (await _subscriptionRepo.FindAsync(
                    s => s.ProjectManagerId == projectManagerId &&
                         s.Status == SubscriptionStatus.Pending)).FirstOrDefault();

                if (existingPending != null)
                {
                    _logger.LogInformation(
                        "Returning existing Pending subscription {Id} for user {UserId} after concurrent request blocked",
                        existingPending.Id, projectManagerId);
                    
                    var existingDto = MapToDto(existingPending);
                    existingDto.ClientSecret = existingPending.ClientSecret;
                    return Result.Success(existingDto);
                }

                _logger.LogWarning(
                    "Duplicate pending subscription attempt for user {UserId} — concurrent request blocked",
                    projectManagerId);
                return Result.Failure<UserSubscriptionDto>(
                    new Error("ConcurrencyError", 
                        ErrorType.Conflict,
                        "A payment is already in progress. Please complete or cancel it first."));
            }

            var resultDto = MapToDto(newSubscription);
            resultDto.ClientSecret = gatewayResult.ClientSecret;

            return Result.Success(resultDto);
        }

        public async Task<Result> UpdateAsync(Guid id, UpdateUserSubscriptionDto dto)
        {
            var sub = await _subscriptionRepo.GetByIdAsync(id);
            if (sub == null)
                return Result.Failure(UserSubscriptionErrors.NotFound);

            if (!Enum.TryParse<SubscriptionStatus>(dto.Status, out var status))
                return Result.Failure(UserSubscriptionErrors.InvalidStatus);

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
                return Result.Failure(UserSubscriptionErrors.NotFound);

            sub.IsDeleted = true;
            _subscriptionRepo.Update(sub);
            return Result.Success();
        }

        public async Task<Result> CancelAsync(Guid id, Guid projectManagerId)
        {
            var sub = await _subscriptionRepo.GetByIdAsync(id);
            if (sub == null)
                return Result.Failure(UserSubscriptionErrors.NotFound);

            if (sub.ProjectManagerId != projectManagerId)
                return Result.Failure(new Error("Forbidden", ErrorType.Failure, "You do not have permission to cancel this subscription."));

            if (sub.Status == SubscriptionStatus.Pending)
            {
                sub.Status = SubscriptionStatus.Canceled;
                _subscriptionRepo.Update(sub);
                await _unitOfWork.SaveChangesAsync();
                return Result.Success();
            }

            if (!string.IsNullOrEmpty(sub.GatewaySubscriptionId))
            {
                var gateway = _gatewayFactory.GetGateway(sub.Gateway);
                var gatewayResult = await gateway.CancelAtPeriodEndAsync(sub.GatewaySubscriptionId, Guid.NewGuid().ToString(), default);
                if (!gatewayResult.IsSuccess)
                    return Result.Failure(new Error("GatewayError", ErrorType.Failure, gatewayResult.ErrorMessage ?? "Failed to cancel subscription at gateway."));
            }

            sub.CancelAtPeriodEnd = true;
            _subscriptionRepo.Update(sub);
            await _unitOfWork.SaveChangesAsync();

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
                TrialEndDate = sub.TrialEndDate,
                CancelAtPeriodEnd = sub.CancelAtPeriodEnd,
                ClientSecret = sub.ClientSecret,
                Gateway = sub.Gateway
            };
        }
    }
}
