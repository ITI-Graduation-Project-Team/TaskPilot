using System.Security.Claims;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Models.Common
{
    /// <summary>
    /// Abstraction over ASP.NET Identity operations.
    /// Lives in Models so both Data (implementation) and Services (consumer) can reference it.
    /// </summary>
    public interface IIdentityService
    {
        Task<Result<User>> FindByEmailAsync(string email);
        Task<Result<User>> CreateUserAsync(User user, string password);
        Task<Result> AddToRoleAsync(User user, string roleName);
        Task<Result<string>> GenerateOTPAsync(User user);
        Task<Result<IEnumerable<Claim>>> GetClaimsAsync(User user);
        Task<Result<IList<string>>>GetRolesAsync(User user);
        Task<Result> DeleteUserAsync(User user);
        Task<Result<string>>VerifyEmailAsync(User user, string otp);
        Task<Result<bool>> CheckPasswordAsync(User user, string password);
    }
}
