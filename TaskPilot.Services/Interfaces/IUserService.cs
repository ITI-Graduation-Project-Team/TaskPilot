using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.DTOs.Users;

namespace TaskPilot.Services.Interfaces
{
    public interface IUserService
    {
        Task<Result<UserDto>> GetByIdAsync(Guid id);
        Task<Result<List<UserDto>>> GetAllAsync();
        Task<Result> DeleteAsync(Guid id);
    }
}
