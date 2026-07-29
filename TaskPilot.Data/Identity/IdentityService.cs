using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskPilot.Data.Context;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using static TaskPilot.Models.Constants.Permissions;

namespace TaskPilot.Data.Identity
{
    public class IdentityService:IIdentityService
    {
        private readonly UserManager<User> _UserManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;

        public IdentityService(UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager, ApplicationDbContext context, SignInManager<User> signInManager)
        {
            _UserManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _signInManager = signInManager;
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
                return new Error("REGISTRATION_VALIDATION_FAILED", ErrorType.Validation, errors);
            }
            return user;
        }
        
        public async Task<Result> AddToRoleAsync(User user, string roleName)
        {
            var roleExist = await _roleManager.RoleExistsAsync(roleName);

            if (!roleExist)
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));

                if (!roleResult.Succeeded)
                {
                    return Result.Failure(CommonErrors.OperationFailed($"Failed to create role: {roleName}"));
                }
            }

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
                return Result.Failure(IdentityErrors.UserDeletionFailed);
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
                return Result.Failure<string>(IdentityErrors.EmailVerificationFailed);
            }
            return Result.Success("Email verified successfully.");
        }

        public async Task<Result> CheckPasswordAsync(User user, string password)
        {
            {
                var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
                if (result.IsLockedOut)
                {
                    return IdentityErrors.AccountLocked;
                }
                if (!result.Succeeded)
                {
                    return CommonErrors.InvalidCredentials();
                }
                return Result.Success();
            }
        }
        public async Task<bool> IsLockedOutAsync(User user)
        {
            return await _UserManager.IsLockedOutAsync(user);
        }
        //public async Task<Result<bool>> CheckPasswordAsync(User user, string password)
        //{
        //   var valid= await _userManager.CheckPasswordAsync(user, password);
        //    if(!valid)
        //    {
        //        return false;
        //    }
        //    return true;
        //}
           //var valid= await _UserManager.CheckPasswordAsync(user, password);
           // if(!valid)
           // {
           //     return false;
           // }
           // return true;
        
        public async Task<Result<User>>GetOrCreateExternalUser(string firstName, string lastName, string email,string provider,string providerKey)
        {
            // 1- has signed with google before
            var existingUser = await _UserManager.FindByLoginAsync(provider,providerKey);
            if (existingUser != null)
            {
                return existingUser;
            }
           var user= await _UserManager.FindByEmailAsync(email);
            // if  has account but  not signed with google before  link his account to google and return the user
            // if not we  create a new user and link it to google and return the user
            //A- new user
            if (user == null)
            {
                var pendingInvite = await _context.EmployeeInvitations
                    .FirstOrDefaultAsync(i => i.Email == email && !i.IsAccepted);

                if (pendingInvite != null)
                {
                    // Create as Employee directly
                    var emp = new Employee
                    {
                        Email = email,
                        UserName = email,
                        FirstNameEn = firstName,
                        LastNameEn = lastName,
                        FirstNameAr = firstName,
                        LastNameAr = lastName,
                        EmailConfirmed = true,
                        CompanyId = pendingInvite.CompanyId
                    };
                    user = emp;
                    var creationResult = await _UserManager.CreateAsync(user);
                    if (!creationResult.Succeeded)
                    {
                        var errors = string.Join(" | ", creationResult.Errors.Select(e => e.Description));
                        return CommonErrors.OperationFailed(errors);
                    }
                    var roleResult = await _UserManager.AddToRoleAsync(user, UserRole.Employee.ToString());
                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                        return CommonErrors.OperationFailed(errors);
                    }
                }
                else
                {
                    // Return Role Selection Required!
                    return AuthErrors.RoleSelectionRequired;
                }
            }
            // B- has account has not signed with google yet or new we'll create a new userlogininfo             
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var updateResult = await _UserManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
                    return CommonErrors.OperationFailed(errors);
                }
            }

            var linkResult = await _UserManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
            if (!linkResult.Succeeded)
            {
                var errors = string.Join(" | ", linkResult.Errors.Select(e => e.Description));
                return CommonErrors.OperationFailed(errors);
            }
            return Result.Success(user);
        }

        public async Task<Result<User>> CompleteExternalUserSignup(string firstName, string lastName, string email, string provider, string providerKey, UserRole role)
        {
            var existingUser = await _UserManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return AuthErrors.EmailAlreadyRegistered;
            }

            User user;
            if (role == UserRole.ProjectManager)
            {
                var freePlan = await _context.SubscriptionPlans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Name == "Free");
                var pm = new ProjectManager
                {
                    Email = email,
                    UserName = email,
                    FirstNameEn = firstName,
                    LastNameEn = lastName,
                    FirstNameAr = firstName,
                    LastNameAr = lastName,
                    EmailConfirmed = true
                };

                if (freePlan != null && !freePlan.IsDeleted)
                {
                    pm.Subscriptions.Add(new UserSubscription
                    {
                        SubscriptionPlanId = freePlan.Id,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddYears(10),
                        BillingCycle = BillingCycle.Monthly,
                        Status = SubscriptionStatus.Active,
                        AutoRenew = true,
                        IsTrial = false
                    });
                }
                user = pm;
            }
            else
            {
                user = new Employee
                {
                    Email = email,
                    UserName = email,
                    FirstNameEn = firstName,
                    LastNameEn = lastName,
                    FirstNameAr = firstName,
                    LastNameAr = lastName,
                    EmailConfirmed = true
                };
            }

            var creationResult = await _UserManager.CreateAsync(user);
            if (!creationResult.Succeeded)
            {
                var errors = string.Join(" | ", creationResult.Errors.Select(e => e.Description));
                return CommonErrors.OperationFailed(errors);
            }

            var roleResult = await _UserManager.AddToRoleAsync(user, role.ToString());
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                return CommonErrors.OperationFailed(errors);
            }

            var linkResult = await _UserManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey, provider));
            if (!linkResult.Succeeded)
            {
                var errors = string.Join(" | ", linkResult.Errors.Select(e => e.Description));
                return CommonErrors.OperationFailed(errors);
            }

            return Result.Success(user);
        }
        public async Task<Result<string>> GeneratePasswordResetTokenAsync(User user)
        {
            var token = await _UserManager.GeneratePasswordResetTokenAsync(user);
            return Result.Success(token);
        }

 
        public async Task<Result>ResetPasswordAsync(User user, string otpCode,string newPassword)
        {
            var result=await _UserManager.ResetPasswordAsync(user, otpCode, newPassword);
            if(!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return Result.Failure(IdentityErrors.PasswordResetValidationFailed(errors));
            }
            return Result.Success();
        }

        public async Task<Result<User>> FindByIdAsync(Guid id)
        {
            var User = await _UserManager
            .FindByIdAsync(id.ToString());
            if (User == null)
            {
                return Result.Failure<User>(CommonErrors.NotFound("User"));
            }
            return Result.Success(User);



        }
    }
}
