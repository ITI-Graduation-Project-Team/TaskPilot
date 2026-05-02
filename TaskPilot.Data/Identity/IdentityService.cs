using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Identity
{
    public class IdentityService:IIdentityService
    {
        private readonly UserManager<User> _UserManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager; // 1. أضف الـ RoleManager

        public IdentityService(UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager)
        {
            _UserManager = userManager;
            _roleManager = roleManager;
        }


        public async Task<Result<User>> FindByEmailAsync(string email)
        {
            var User = await _UserManager.FindByEmailAsync(email);
            if(User == null)
            {
                return Result.Failure<User>(CommonErrors.NotFound("User"));
            }
            return Result.Success(User);
        }
        public async Task<Result<User>> CreateUserAsync(User user, string password)
        {
            var creationResult = await _UserManager.CreateAsync(user,password);
            if (!creationResult.Succeeded)
            {
                var errors = string.Join(" | ", creationResult.Errors.Select(e => e.Description));
                return CommonErrors.InvalidInput(errors);
                //return CommonErrors.InvalidInput(c.Errors.First().Description);
            }
            return user;
        }
        //public async Task<Result> AddToRoleAsync(User user, string roleName)
        //{
        //    var addingResult = await _UserManager.AddToRoleAsync(user, roleName);
        //    if (!addingResult.Succeeded)
        //    {
        //        var errors = string.Join(" | ", addingResult.Errors.Select(e => e.Description));
        //        return Result.Failure(CommonErrors.InvalidInput(errors));
        //        //return Result.Failure(CommonErrors.InvalidInput(addingResult.Errors.First().Description));
        //    }
        //    return Result.Success();
        //}
        public async Task<Result> AddToRoleAsync(User user, string roleName)
        {
            // 3. التحقق من وجود الـ Role في قاعدة البيانات
            var roleExist = await _roleManager.RoleExistsAsync(roleName);

            if (!roleExist)
            {
                // 4. إذا لم تكن موجودة، قم بإنشائها فوراً
                var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));

                if (!roleResult.Succeeded)
                {
                    return Result.Failure(CommonErrors.OperationFailed($"Failed to create role: {roleName}"));
                }
            }

            // 5. إضافة المستخدم للدور (سواء كان موجوداً من قبل أو تم إنشاؤه الآن)
            var addingResult = await _UserManager.AddToRoleAsync(user, roleName);

            if (!addingResult.Succeeded)
            {
                var errors = string.Join(" | ", addingResult.Errors.Select(e => e.Description));
                return Result.Failure(CommonErrors.InvalidInput(errors));
            }

            return Result.Success();
        }

        public async Task<Result> DeleteUserAsync(User user)
        {
            var res = await _UserManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                return Result.Failure(CommonErrors.OperationFailed("Failed to Delete User"));
            }
            return Result.Success();
        }
        public async Task<Result<string>> GenerateOTPAsync(User user)
        {
       
            var otp = await _UserManager.GenerateEmailConfirmationTokenAsync(user);
            return Result.Success(otp);
        }

        public async Task<Result<IEnumerable<Claim>>> GetClaimsAsync(User user)
        {
            var claims = await _UserManager.GetClaimsAsync(user);
            return Result.Success<IEnumerable<Claim>>(claims);
        }

        public async Task<Result<IList<string>>> GetRolesAsync(User user)
        {
            var roles = await _UserManager.GetRolesAsync(user);
            return Result.Success(roles);
        }

        public async Task<Result<string>> VerifyEmailAsync(User user, string otp)
        {
            var result = await _UserManager.ConfirmEmailAsync(user, otp);
             if(!result.Succeeded)
            {
                return Result.Failure<string>(CommonErrors.OperationFailed("Email verification failed."));
            }
            return Result.Success("Email verified successfully.");
        }

        public async Task<Result<bool>> CheckPasswordAsync(User user, string password)
        {
           var valid= await _UserManager.CheckPasswordAsync(user, password);
            if(!valid)
            {
                return false;
            }
            return true;
        }
    }
}
