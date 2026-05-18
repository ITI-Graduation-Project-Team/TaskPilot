using TaskPilot.Data.Context;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using TaskPilot.DTOs.Users;

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
        private readonly ILocalizationService _localizationService;

        public UserService(ApplicationDbContext applicationDbContext, ILocalizationService localizationService)
        {
            _context = applicationDbContext;
            _localizationService = localizationService;
        }

        public async Task<Result<UserDto>> GetByIdAsync(Guid id)
        {
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var user = await _context.Users
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
                return Result.Failure<UserDto>(CommonErrors.NotFound("User"));

            return Result.Success(user);
        }

        public async Task<Result<List<UserDto>>> GetAllAsync()
        {
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var users = await _context.Users
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
            var user = await _context.Users.FindAsync(id);

            if (user is null)
                return Result.Failure(CommonErrors.NotFound("User"));

            _context.Users.Remove(user);
            return Result.Success();
        }
    }
}
