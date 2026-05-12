using System.Collections.Generic;
using System.Threading.Tasks;
using TaskPilot.DTOs.SubscriptionPlans;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ISubscriptionPlanService
    {
        Task<Result<SubscriptionPlanDto>> GetByIdAsync(int id);
        Task<Result<List<SubscriptionPlanDto>>> GetAllAsync();
        Task<Result<SubscriptionPlanDto>> CreateAsync(CreateSubscriptionPlanDto dto);
        Task<Result> UpdateAsync(int id, UpdateSubscriptionPlanDto dto);
        Task<Result> DeleteAsync(int id);
    }
}
