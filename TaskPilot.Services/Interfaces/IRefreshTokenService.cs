using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<Result<string>> GenerateAsync(User user);

        Task<Result<User>> ValidateAsync(string token);


        Task<Result> RevokeAsync(string token);

        Task RevokeAllAsync(Guid userId);
    }
}   
