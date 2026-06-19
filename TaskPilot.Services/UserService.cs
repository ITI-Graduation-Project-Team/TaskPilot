using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Users;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    /// <summary>
    /// Contains all business logic for User operations.
    /// Accesses data exclusively through IUnitOfWork — never touches DbContext directly.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class UserService : IUserService
    {
        //private readonly ApplicationDbContext _context;
        private readonly IRepository<User> _userRepo;
        private readonly ILocalizationService _localizationService;

        public UserService(/*ApplicationDbContext applicationDbContext,*/ ILocalizationService localizationService, IRepository<User> UserRepo)
        {
            //_context = applicationDbContext;
            _localizationService = localizationService;
            _userRepo = UserRepo;
        }

        public async Task<Result<UserDto>> GetByIdAsync(Guid id)
        {
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var user = await _userRepo.GetQueryable()
                .Where(u => u.Id == id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FirstName = isArabic ? u.FirstNameAr : u.FirstNameEn,
                    LastName = isArabic ? u.LastNameAr : u.LastNameEn,
                    CompanyId = u.CompanyId,
                    IsDeleted = u.IsDeleted
                })
                .FirstOrDefaultAsync();

            if (user is null)
                return Result.Failure<UserDto>(UserErrors.NotFound);

            return Result.Success(user);
        }

        public async Task<Result<List<UserDto>>> GetAllAsync()
        {
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var users = await _userRepo.GetQueryable()
                .Where(u => !u.IsDeleted)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FirstName = isArabic ? u.FirstNameAr : u.FirstNameEn,
                    LastName = isArabic ? u.LastNameAr : u.LastNameEn,
                    CompanyId = u.CompanyId,
                    IsDeleted = u.IsDeleted
                })
                .ToListAsync();

            return Result.Success(users);
        }
        public async Task<Result> DeleteAsync(Guid id)
        {
            var user = await _userRepo.GetByIdAsync(id);

            if (user is null)
                return Result.Failure(UserErrors.NotFound);

            user.IsDeleted = true;
            _userRepo.Update(user);
            return Result.Success();
        }
    }
}
