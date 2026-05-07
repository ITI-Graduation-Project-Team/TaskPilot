using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskPilot.DTOs.UserSubscriptions;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IUserSubscriptionService
    {
        Task<Result<UserSubscriptionDto>> GetByIdAsync(Guid id);
        Task<Result<List<UserSubscriptionDto>>> GetAllAsync(Guid? projectManagerId = null);
        Task<Result<UserSubscriptionDto>> GetCurrentSubscriptionAsync(Guid projectManagerId);
        Task<Result<UserSubscriptionDto>> CreateAsync(Guid projectManagerId, CreateUserSubscriptionDto dto);
        Task<Result> UpdateAsync(Guid id, UpdateUserSubscriptionDto dto);
        Task<Result> DeleteAsync(Guid id);
    }
}
