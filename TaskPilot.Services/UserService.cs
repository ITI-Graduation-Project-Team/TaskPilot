using TaskPilot.Data.Context;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Services
{
    /// <summary>
    /// Contains all business logic for User operations.
    /// Accesses data exclusively through IUnitOfWork — never touches DbContext directly.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext applicationDbContext)
        {
            _context = applicationDbContext;
        }

        public async Task<Result<User>> GetByIdAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user is null)
                return Result.Failure<User>(CommonErrors.NotFound("User"));

            return Result.Success(user);
        }

        public async Task<Result<List<User>>> GetAllAsync()
        {
            var users = await _context.Users
                .Where(u => !u.IsDeleted)
                .ToListAsync();
            return Result.Success(users);
        }
        public async Task<Result> DeleteAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user is null)
                return Result.Failure(CommonErrors.NotFound("User"));

            _context.Users.Remove(user);
            return Result.Success();
        }
    }
}
