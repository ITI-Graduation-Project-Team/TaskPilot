using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Application.Common.Errors;
using TaskPilot.Application.Common.Results;
using TaskPilot.Application.Interfaces;
using TaskPilot.Domain.Entities;

namespace TaskPilot.Infrastructure.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<ApplicationUser> _UserManager;

        public IdentityService(UserManager<ApplicationUser> userManager)
        {
            _UserManager = userManager;
        }


        public async Task<Guid?> FindByEmailAsync(string email)
        {
            var User = await _UserManager.FindByEmailAsync(email);
            return User?.Id;    
        }
        public async Task<Result<Guid>> CreateUserAsync(string email, string password)
        {
            var ApplicationUser = new ApplicationUser
            {
                UserName = email,
                Email = email
            };
            var res=await _UserManager.CreateAsync(ApplicationUser);
            if (!res.Succeeded)
            {
                return Result<Guid>.Failure(CommonErrors.InvalidInput(res.Errors.First().Description));
            }
            return Result.Success(ApplicationUser.Id);
        }
       public async Task<Result> AddToRoleAsync(Guid userId, string roleName)
        {
            var User = await _UserManager.FindByIdAsync(userId.ToString());
            if (User == null)
            {
                return Result.Failure(CommonErrors.NotFound("User"));
            }
            var res = await _UserManager.AddToRoleAsync(User, roleName);
            if (!res.Succeeded)
            {
                return Result.Failure(CommonErrors.InvalidInput(res.Errors.First().Description));
            }
            return Result.Success();
        }

        public async Task<Result<string>> GenerateOTPAsync(string email)
        {
            var user = await _UserManager.FindByEmailAsync(email);

            if (user == null)
            {
                return Result.Failure<string>(CommonErrors.NotFound("User"));
            }

            var otp = await _UserManager.GenerateEmailConfirmationTokenAsync(user);
            return Result.Success(otp);
        }
    }
}
