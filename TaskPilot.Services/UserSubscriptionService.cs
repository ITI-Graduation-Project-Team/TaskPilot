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

        public UserSubscriptionService(
            IRepository<UserSubscription> subscriptionRepo,
            IRepository<SubscriptionPlan> planRepo,
            IRepository<ProjectManager> pmRepo)
        {
            _subscriptionRepo = subscriptionRepo;
            _planRepo = planRepo;
            _pmRepo = pmRepo;
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
                s => s.ProjectManagerId == projectManagerId && s.Status == SubscriptionStatus.Active, 
                s => s.Plan))
                .OrderByDescending(s => s.EndDate)
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

            // Expire current active subscriptions
            var activeSubs = await _subscriptionRepo.FindAsync(s => s.ProjectManagerId == projectManagerId && s.Status == SubscriptionStatus.Active);
            foreach (var sub in activeSubs)
            {
                sub.Status = SubscriptionStatus.Expired;
                _subscriptionRepo.Update(sub);
            }

            var endDate = billingCycle == BillingCycle.Monthly ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddYears(1);

            var newSubscription = new UserSubscription
            {
                ProjectManagerId = projectManagerId,
                SubscriptionPlanId = dto.SubscriptionPlanId,
                StartDate = DateTime.UtcNow,
                EndDate = endDate,
                BillingCycle = billingCycle,
                Status = SubscriptionStatus.Active,
                AutoRenew = dto.AutoRenew,
                IsTrial = false
            };

            await _subscriptionRepo.AddAsync(newSubscription);

            var resultDto = new UserSubscriptionDto
            {
                ProjectManagerId = projectManagerId,
                SubscriptionPlanId = plan.Id,
                PlanName = plan.Name,
                StartDate = newSubscription.StartDate,
                EndDate = newSubscription.EndDate,
                BillingCycle = newSubscription.BillingCycle.ToString(),
                Status = newSubscription.Status.ToString(),
                AutoRenew = newSubscription.AutoRenew,
                IsTrial = newSubscription.IsTrial
            };

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
