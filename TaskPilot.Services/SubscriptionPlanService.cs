using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.SubscriptionPlans;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly IRepository<SubscriptionPlan> _repository;

        public SubscriptionPlanService(IRepository<SubscriptionPlan> repository)
        {
            _repository = repository;
        }

        public async Task<Result<SubscriptionPlanDto>> GetByIdAsync(int id)
        {
            var plan = await _repository.FindSingleAsync(p => p.Id == id);
            if (plan == null)
                return Result.Failure<SubscriptionPlanDto>(CommonErrors.NotFound("SubscriptionPlan"));

            return Result.Success(MapToDto(plan));
        }

        public async Task<Result<List<SubscriptionPlanDto>>> GetAllAsync()
        {
            var plans = await _repository.GetAllAsync();
            return Result.Success(plans.Select(MapToDto).ToList());
        }

        public async Task<Result<SubscriptionPlanDto>> CreateAsync(CreateSubscriptionPlanDto dto)
        {
            var exists = await _repository.AnyAsync(p => p.Name.ToLower() == dto.Name.ToLower());
            if (exists)
                return Result.Failure<SubscriptionPlanDto>(CommonErrors.Conflict("Subscription plan with the same name already exists."));

            var plan = new SubscriptionPlan
            {
                Name = dto.Name,
                MonthlyPrice = dto.MonthlyPrice,
                AnnualPrice = dto.AnnualPrice,
                Currency = dto.Currency,
                MaxProjects = dto.MaxProjects,
                MaxUsersPerProject = dto.MaxUsersPerProject,
                HasAi = dto.HasAi,
                HasAdvancedAnalytics = dto.HasAdvancedAnalytics,
                HasTrial = dto.HasTrial,
                TrialDays = dto.TrialDays
            };

            await _repository.AddAsync(plan);
            
            // To return ID we have to map the newly created entity. Note: Id will be 0 until SaveChanges is called by UoW in controller.
            return Result.Success(MapToDto(plan));
        }

        public async Task<Result> UpdateAsync(int id, UpdateSubscriptionPlanDto dto)
        {
            var existing = await _repository.FindSingleAsync(p => p.Id == id);
            if (existing == null)
                return Result.Failure(CommonErrors.NotFound("SubscriptionPlan"));

            var nameExists = await _repository.AnyAsync(p => p.Name.ToLower() == dto.Name.ToLower() && p.Id != id);
            if (nameExists)
                return Result.Failure(CommonErrors.Conflict("Another subscription plan with the same name already exists."));

            existing.Name = dto.Name;
            existing.MonthlyPrice = dto.MonthlyPrice;
            existing.AnnualPrice = dto.AnnualPrice;
            existing.Currency = dto.Currency;
            existing.MaxProjects = dto.MaxProjects;
            existing.MaxUsersPerProject = dto.MaxUsersPerProject;
            existing.HasAi = dto.HasAi;
            existing.HasAdvancedAnalytics = dto.HasAdvancedAnalytics;
            existing.HasTrial = dto.HasTrial;
            existing.TrialDays = dto.TrialDays;

            _repository.Update(existing);
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(int id)
        {
            var plan = await _repository.FindSingleAsync(p => p.Id == id);
            if (plan == null)
                return Result.Failure(CommonErrors.NotFound("SubscriptionPlan"));

            plan.IsDeleted = true;
            _repository.Update(plan);

            return Result.Success();
        }

        private static SubscriptionPlanDto MapToDto(SubscriptionPlan plan)
        {
            return new SubscriptionPlanDto
            {
                Id = plan.Id,
                Name = plan.Name,
                MonthlyPrice = plan.MonthlyPrice,
                AnnualPrice = plan.AnnualPrice,
                Currency = plan.Currency,
                MaxProjects = plan.MaxProjects,
                MaxUsersPerProject = plan.MaxUsersPerProject,
                HasAi = plan.HasAi,
                HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
                HasTrial = plan.HasTrial,
                TrialDays = plan.TrialDays
            };
        }
    }
}
