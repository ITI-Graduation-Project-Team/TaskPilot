using TaskPilot.Models.Common.Results;

namespace TaskPilot.Models.Common
{
    /// <summary>
    /// Abstraction over ASP.NET Identity operations.
    /// Lives in Models so both Data (implementation) and Services (consumer) can reference it.
    /// </summary>
    public interface IIdentityService
    {
        Task<Guid?> FindByEmailAsync(string email);
        Task<Result<Guid>> CreateUserAsync(string email, string password);
        Task<Result> AddToRoleAsync(Guid userId, string roleName);
        Task<Result<string>> GenerateOTPAsync(string email);
    }
}
