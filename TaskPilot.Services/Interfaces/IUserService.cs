using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    /// <summary>
    /// Business logic contract for User-related operations.
    /// Implemented in the Application.Services layer.
    /// </summary>
    public interface IUserService
    {
        Task<Result<User>> GetByIdAsync(Guid id);
        Task<Result<IEnumerable<User>>> GetAllAsync();
        //Task<Result<User>> GetByApplicationUserIdAsync(Guid applicationUserId);
        Task<Result> DeleteAsync(Guid id);
    }
}
