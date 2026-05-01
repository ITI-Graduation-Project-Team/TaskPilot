using Microsoft.AspNetCore.Identity;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Auth;
using TaskPilot.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services
{
    /// <summary>
    /// Implements <see cref="IAuthService"/> using ASP.NET Core Identity.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUnitOfWork _unitOfWork;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<AuthResponseDTO>> RegisterAsync(RegisterDTO dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser is not null)
                return Result.Failure<AuthResponseDTO>(
                    CommonErrors.Conflict($"A user with email '{dto.Email}' already exists."));

            var companyExists = await _unitOfWork.Companies.AnyAsync(c => c.Id == dto.CompanyId);
            if (!companyExists)
                return Result.Failure<AuthResponseDTO>(CommonErrors.NotFound("Company"));

            var applicationUser = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email
            };

            var identityResult = await _userManager.CreateAsync(applicationUser, dto.Password);
            if (!identityResult.Succeeded)
            {
                var errorDescription = string.Join("; ", identityResult.Errors.Select(e => e.Description));
                return Result.Failure<AuthResponseDTO>(CommonErrors.InvalidInput(errorDescription));
            }

            var domainUser = new User
            {
                FirstNameEn = dto.FirstNameEn,
                LastNameEn = dto.LastNameEn,
                FirstNameAr = dto.FirstNameAr,
                LastNameAr = dto.LastNameAr,
                CompanyId = dto.CompanyId,
                ApplicationUserId = applicationUser.Id
            };

            await _unitOfWork.Users.AddAsync(domainUser);
            // Note: Controller must call _unitOfWork.SaveChangesAsync() after this

            return Result.Success(new AuthResponseDTO
            {
                UserId = domainUser.Id,
                Email = dto.Email,
                FullName = $"{dto.FirstNameEn} {dto.LastNameEn}",
                Token = string.Empty // TODO: Generate JWT token
            });
        }

        public async Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto)
        {
            var applicationUser = await _userManager.FindByEmailAsync(dto.Email);
            if (applicationUser is null)
                return Result.Failure<AuthResponseDTO>(CommonErrors.InvalidCredentials());

            var signInResult = await _signInManager
                .CheckPasswordSignInAsync(applicationUser, dto.Password, lockoutOnFailure: false);

            if (!signInResult.Succeeded)
                return Result.Failure<AuthResponseDTO>(CommonErrors.InvalidCredentials());

            var domainUser = await _unitOfWork.Users
                .FindSingleAsync(u => u.ApplicationUserId == applicationUser.Id);

            if (domainUser is null)
                return Result.Failure<AuthResponseDTO>(CommonErrors.NotFound("User profile"));

            return Result.Success(new AuthResponseDTO
            {
                UserId = domainUser.Id,
                Email = applicationUser.Email!,
                FullName = $"{domainUser.FirstNameEn} {domainUser.LastNameEn}",
                Token = string.Empty // TODO: Generate JWT token
            });
        }
    }
}
