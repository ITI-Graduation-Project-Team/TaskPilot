using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Application.Common.Results;

namespace TaskPilot.Application.Interfaces
{
    public interface IIdentityService
    {
        Task<Guid?> FindByEmailAsync(string email);
        Task<Result<Guid>> CreateUserAsync(string email, string password);
        Task<Result> AddToRoleAsync(Guid userId, string roleName);
        Task<Result<string>>GenerateOTPAsync(string email);
    }
}
