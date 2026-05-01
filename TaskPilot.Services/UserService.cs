using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services
{
    /// <summary>
    /// Contains all business logic for User operations.
    /// Accesses data exclusively through IUnitOfWork — never touches DbContext directly.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<User>> GetByIdAsync(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);

            if (user is null)
                return Result.Failure<User>(CommonErrors.NotFound("User"));

            return Result.Success(user);
        }

        public async Task<Result<IEnumerable<User>>> GetAllAsync()
        {
            var users = await _unitOfWork.Users.GetAllAsync();
            return Result.Success(users);
        }

        public async Task<Result<User>> GetByApplicationUserIdAsync(Guid applicationUserId)
        {
            var user = await _unitOfWork.Users
                .FindSingleAsync(u => u.ApplicationUserId == applicationUserId);

            if (user is null)
                return Result.Failure<User>(CommonErrors.NotFound("User"));

            return Result.Success(user);
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);

            if (user is null)
                return Result.Failure(CommonErrors.NotFound("User"));

            _unitOfWork.Users.Delete(user);
            return Result.Success();
        }
    }
}
