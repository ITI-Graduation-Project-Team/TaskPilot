using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    public interface IUserService
    {
        Task<Result<User>> GetByIdAsync(Guid id);
        Task<Result<List<User>>> GetAllAsync();
        Task<Result> DeleteAsync(Guid id);
    }
}
