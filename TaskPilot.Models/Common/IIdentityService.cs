using System.Security.Claims;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Common
{
    public interface IIdentityService
    {
        Task<Result<User>> FindByEmailAsync(string email);
        Task<Result<User>> FindByIdAsync(Guid id);
        Task<Result<User>> CreateUserAsync(User user, string password);
        Task<Result> AddToRoleAsync(User user, string roleName);
        Task<Result<string>> GenerateOTPAsync(User user);
        Task<Result<IEnumerable<Claim>>> GetClaimsAsync(User user);
        Task<Result<IList<string>>>GetRolesAsync(User user);
        Task<Result> DeleteUserAsync(User user);
        Task<Result<string>>VerifyEmailAsync(User user, string otp);
        Task<Result> CheckPasswordAsync(User user, string password);
        Task<bool> IsLockedOutAsync(User user);
        Task<Result<User>> GetOrCreateExternalUser(string firstName, string lastName, string email, string provider, string providerKey);
        Task<Result<string>>GeneratePasswordResetTokenAsync(User user);
        Task<Result>ResetPasswordAsync(User user, string token, string newPassword);
    }
}
