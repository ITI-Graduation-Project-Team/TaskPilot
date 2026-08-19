using TaskPilot.Data.Repositories;
using TaskPilot.DTOs;
using TaskPilot.DTOs.Auth;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces.External;
using TaskPilot.Services.Interfaces;
using Microsoft.AspNetCore.Identity.Data;

namespace TaskPilot.Services
{
    public class AuthService : IAuthService
    {
        private readonly IIdentityService _identityService;
        private readonly IEmailService _emailService;
        private readonly IEmailBodyService _emailBodyService;
        private readonly ITokenService _tokenService;
        private readonly IGoogleAuthService _googleAuthService;

        private readonly IRepository<EmployeeInvitation> _invitationRepository;
        private readonly TaskPilot.Models.Common.ILocalizationService _localizationService;

        private readonly IRefreshTokenService _refreshTokenService;
        public AuthService(
            IIdentityService identityService,
            IEmailService emailService,
            IEmailBodyService emailBodyService,
            ITokenService tokenService,
            IGoogleAuthService googleAuthService,

            IRepository<EmployeeInvitation> invitationRepository,
            TaskPilot.Models.Common.ILocalizationService localizationService,
            IRefreshTokenService refreshTokenService)
        {
            _identityService = identityService;
            _emailService = emailService;
            _emailBodyService = emailBodyService;
            _tokenService = tokenService;
            _googleAuthService = googleAuthService;

            _invitationRepository = invitationRepository;
            _localizationService = localizationService;
            _refreshTokenService = refreshTokenService;
        }

        public async Task<Result> RegisterAsync(RegisterDTO RegisterRequest, UserRole Role)
        {
            if (Role != UserRole.ProjectManager && Role != UserRole.Employee)
            {
                return AuthErrors.InvalidRoleRegistration;
            }

            //1 check 
            var existingUser = await _identityService.FindByEmailAsync(RegisterRequest.Email);
            if (existingUser.IsSuccess)
            {
                return AuthErrors.EmailAlreadyRegistered;
            }
            //2 create
            User user;

            if (Role == UserRole.ProjectManager)
            {
                var pm = new ProjectManager
                {
                    Email = RegisterRequest.Email,
                    FirstNameAr = RegisterRequest.FirstNameAr,
                    LastNameAr = RegisterRequest.LastNameAr,
                    FirstNameEn = RegisterRequest.FirstNameEn,
                    LastNameEn = RegisterRequest.LastNameEn,
                    UserName = RegisterRequest.Email,
                };


                user = pm;
            }
            else
            {
                user = new Employee
                {
                    Email = RegisterRequest.Email,
                    FirstNameAr = RegisterRequest.FirstNameAr,
                    LastNameAr = RegisterRequest.LastNameAr,
                    FirstNameEn = RegisterRequest.FirstNameEn,
                    LastNameEn = RegisterRequest.LastNameEn,
                    UserName = RegisterRequest.Email,
                };
            }
            var CreatedUser = await _identityService.CreateUserAsync(user, RegisterRequest.Password);
            if (CreatedUser.IsFailure)
            {
                return Result.Failure(CreatedUser.Error);
            }
            //3 add to role
            var addToRoleResult = await _identityService.AddToRoleAsync(CreatedUser.Value, Role.ToString());
            if (addToRoleResult.IsFailure)
            {
                await _identityService.DeleteUserAsync(CreatedUser.Value);
                return Result.Failure(addToRoleResult.Error);
            }
            // send confirmation email
            await SendConfirmationEmailAsync(CreatedUser.Value);
            return Result.Success();
        }
        public async Task<Result> ResendConfirmationEmailAsync(string email)
        {
            var userResult = await _identityService.FindByEmailAsync(email);
            if (userResult.IsFailure)
            {
                return Result.Success();
            }
            var user = userResult.Value;

            if (user.EmailConfirmed)
             return AuthErrors.EmailAlreadyVerified;

            return await SendConfirmationEmailAsync(user);
        }
        public async Task<Result<AuthResponseDTO>> ConfirmEmailAsync(ConfirmEmailDTO confirmEmailDTO)
        {
            //1
            var userResult = await _identityService.FindByEmailAsync(confirmEmailDTO.Email);
            if (userResult.IsFailure)
            {
                return AuthErrors.UserNotFound;
            }
            //2
            var user = userResult.Value;
            var verifyResult = await _identityService.VerifyEmailAsync(user, confirmEmailDTO.OTP);

            if (verifyResult.IsFailure)
            {
                return verifyResult.Error;
            }
            var token = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _refreshTokenService.GenerateAsync(user);
            var roles = (await _identityService.GetRolesAsync(user)).Value.ToList();
            if (refreshToken.IsFailure)
            {
                return Result.Failure<AuthResponseDTO>(refreshToken.Error);

            }
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var response = new AuthResponseDTO
            {
                Email = confirmEmailDTO.Email,
                FullName = isArabic ? $"{user.FirstNameAr} {user.LastNameAr}".Trim() : $"{user.FirstNameEn} {user.LastNameEn}".Trim(),
                RefreshToken = refreshToken.Value,
                Roles = roles,
                Token = token,
                UserId = userResult.Value.Id,
                IsProfileCompleted = user is Employee emp ? emp.IsProfileCompleted : user.CompanyId.HasValue
            };

            return response;
        }

        public async Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO loginDTO)
        {
            var userResult = await _identityService.FindByEmailAsync(loginDTO.Email);
            if (userResult.IsFailure)
            {
                return CommonErrors.InvalidCredentials();
            }
            var user = userResult.Value;
            if (!user.EmailConfirmed)
            {
                return AuthErrors.EmailNotConfirmed;
            }
            var passwordCheckResult = await _identityService.CheckPasswordAsync(user, loginDTO.Password);
            if (passwordCheckResult.IsFailure)
            {
                return Result.Failure<AuthResponseDTO>(passwordCheckResult.Error);
            }
            var roles = (await _identityService.GetRolesAsync(user)).Value.ToList();
            var token = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _refreshTokenService.GenerateAsync(user);
            if (refreshToken.IsFailure)
                return Result.Failure<AuthResponseDTO>(refreshToken.Error);
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var response = new AuthResponseDTO
            {
                Email = user.Email,
                FullName = isArabic ? $"{user.FirstNameAr} {user.LastNameAr}".Trim() : $"{user.FirstNameEn} {user.LastNameEn}".Trim(),
                Token = token,
                RefreshToken = refreshToken.Value,
                UserId = user.Id,
                Roles = roles,
                IsProfileCompleted = user is Employee emp ? emp.IsProfileCompleted : user.CompanyId.HasValue
            };
            return response;
        }
        public async Task<Result<AuthResponseDTO>> RefreshTokenAsync(RefreshTokenDTO refreshTokenDto)
        {
            var validateResult = await _refreshTokenService.ValidateAsync(refreshTokenDto.RefreshToken);
            if (validateResult.IsFailure)
                return Result.Failure<AuthResponseDTO>(validateResult.Error);
            var user = validateResult.Value;
            var roles = (await _identityService.GetRolesAsync(user)).Value.ToList();

            var newAccessToken = await _tokenService.GenerateAccessToken(user);
            var newRefreshToken = await _refreshTokenService.GenerateAsync(user);
            if (newRefreshToken.IsFailure)
                return Result.Failure<AuthResponseDTO>(newRefreshToken.Error);
            var response = new AuthResponseDTO
            {
                UserId = user.Id,
                Email = user.Email,
                Token = newAccessToken,
                Roles = roles,
                RefreshToken = newRefreshToken.Value,
            };
            return response;
        }
        public async Task<Result> LogoutAsync(string Token)
        {
            var result = await _refreshTokenService.RevokeAsync(Token);
            if (result.IsFailure)
                return Result.Failure(result.Error);
            return Result.Success();
        }

        private async Task<Result> SendConfirmationEmailAsync(User user)
        {
            //1
            if (user.EmailConfirmed)
            {
                return AuthErrors.EmailAlreadyVerified;
            }
            //2
            var OtpResult = await _identityService.GenerateOTPAsync(user);
            if (OtpResult.IsFailure)
            {
                return AuthErrors.OtpGenerationFailed;
            }
            //3
            var name = $"{user.FirstNameEn}  {user.LastNameEn}";
            var EmailBody =
             _emailBodyService.GenerateConfirmationEmailBody(name, user.Email, OtpResult.Value);
            var emailRequest = new EmailRequest
            {
                To = user.Email,
                Subject = "Email Confirmation",
                Body = EmailBody
            };
            var EmailResult = await _emailService.SendEmailAsync(emailRequest);
            //if (EmailResult.IsFailure)
            //{
            //    return CommonErrors.OperationFailed("We couldn't send the confirmation email. Please try resending it.");
            //}
            return Result.Success();
        }

        public async Task<Result<AuthResponseDTO>> GoogleLoginAsync(string idToken)
        {
            //1- check valid google token and get user info
            var googleResult = await _googleAuthService.ValidateTokenAsync(idToken);
            if (googleResult.IsFailure)
            {
                return googleResult.Error;
            }
            var googleUser = googleResult.Value;
            //2-create or get user in our system

            var userResult = await _identityService.GetOrCreateExternalUser(googleUser.FirstName, googleUser.LastName, googleUser.Email, "Google", googleUser.GoogleId);
            if (userResult.IsFailure)
            {
                if (userResult.Error.Code == AuthErrors.RoleSelectionRequired.Code)
                {
                    return Result.Success(new AuthResponseDTO
                    {
                        RequireRoleSelection = true,
                        Email = googleUser.Email,
                        FullName = $"{googleUser.FirstName} {googleUser.LastName}".Trim()
                    });
                }
                return userResult.Error;
            }
            var user = userResult.Value;
            //3-generate our token
            var token = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _refreshTokenService.GenerateAsync(user);
            var roles = (await _identityService.GetRolesAsync(user)).Value.ToList();

            if (refreshToken.IsFailure)
                return Result.Failure<AuthResponseDTO>(refreshToken.Error);
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var response = new AuthResponseDTO
            {
                Email = user.Email,
                FullName = isArabic ? $"{user.FirstNameAr} {user.LastNameAr}".Trim() : $"{user.FirstNameEn} {user.LastNameEn}".Trim(),
                Token = token,
                Roles = roles,
                UserId = user.Id,
                RefreshToken = refreshToken.Value,
                IsProfileCompleted = user is Employee emp ? emp.IsProfileCompleted : user.CompanyId.HasValue
            };
            return response;
        }

        public async Task<Result<AuthResponseDTO>> CompleteGoogleSignupAsync(string idToken, UserRole role)
        {
            if (role != UserRole.ProjectManager && role != UserRole.Employee)
            {
                return Result.Failure<AuthResponseDTO>(AuthErrors.InvalidRoleRegistration);
            }

            var googleResult = await _googleAuthService.ValidateTokenAsync(idToken);
            if (googleResult.IsFailure)
            {
                return googleResult.Error;
            }
            var googleUser = googleResult.Value;

            var userResult = await _identityService.CompleteExternalUserSignup(
                googleUser.FirstName,
                googleUser.LastName,
                googleUser.Email,
                "Google",
                googleUser.GoogleId,
                role
            );

            if (userResult.IsFailure)
            {
                return userResult.Error;
            }

            var user = userResult.Value;

            var token = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _refreshTokenService.GenerateAsync(user);
            var roles = (await _identityService.GetRolesAsync(user)).Value.ToList();

            if (refreshToken.IsFailure)
                return Result.Failure<AuthResponseDTO>(refreshToken.Error);

            bool isArabic = _localizationService.CurrentLanguage == "ar";

            return new AuthResponseDTO
            {
                Email = user.Email,
                FullName = isArabic ? $"{user.FirstNameAr} {user.LastNameAr}".Trim() : $"{user.FirstNameEn} {user.LastNameEn}".Trim(),
                Token = token,
                Roles = roles,
                UserId = user.Id,
                RefreshToken = refreshToken.Value,
                IsProfileCompleted = user is Employee emp ? emp.IsProfileCompleted : user.CompanyId.HasValue
            };
        }

        public async Task<Result> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var userResult = await _identityService.FindByEmailAsync(dto.Email);
            if (userResult.IsFailure)
            {
                return Result.Success();
            }
            var user = userResult.Value;
            var resetTokenResult = await _identityService.GeneratePasswordResetTokenAsync(user);
            if (resetTokenResult.IsFailure)
            {
                return AuthErrors.PasswordResetFailed;
            }
            var resetToken = resetTokenResult.Value;
            var name = $"{user.FirstNameEn}  {user.LastNameEn}";
            var EmailBody =
             _emailBodyService.GeneratePasswordResetEmailBody(name, user.Email, resetToken);
            var emailRequest = new EmailRequest
            {
                To = user.Email,
                Subject = "Password Reset",
                Body = EmailBody
            };
            var EmailResult = await _emailService.SendEmailAsync(emailRequest);
            return Result.Success();

        }
        public async Task<Result> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO)
        {

            var userResult = await _identityService.FindByEmailAsync(resetPasswordDTO.Email);
            if (userResult.IsFailure)
            {
                return AuthErrors.UserNotFound;
            }
            var user = userResult.Value;
            var resetResult = await _identityService.ResetPasswordAsync(user, resetPasswordDTO.OTP, resetPasswordDTO.Password);
            if (resetResult.IsFailure)
            {
                return resetResult.Error;
            }
            await _refreshTokenService.RevokeAllAsync(user.Id);
            return Result.Success();
        }
        public async Task<
    Result<InvitationInfoResponse>>
    GetInvitationInfoAsync(
        string token)
        {


            var invitation =
            await _invitationRepository
            .FindSingleAsync(
                     x => x.Token == token,
                     includes: x => x.Company);

            if (invitation is null)
            {
                return CommonErrors.NotFound(
                    "Invitation");
            }

            // Expired

            if (invitation.ExpiresAt
                < DateTime.UtcNow)
            {
                return CompanyErrors
                    .InvitationExpired;
            }

            // Already Accepted

            if (invitation.IsAccepted)
            {
                return CompanyErrors
                    .InvitationAlreadyAccepted;
            }

            // Existing User

            var existingUser =
                await _identityService
                    .FindByEmailAsync(
                        invitation.Email);

            return new InvitationInfoResponse
            {
                Email = invitation.Email,

                CompanyName =
                    invitation.Company.Name,

                UserExists =
                    existingUser.IsSuccess,

                Token = token
            };
        }

        public async Task<Result>
    CompleteInvitationAsync(
        string token,
        Guid userId)
        {
            // Invitation

            var invitation =
                await _invitationRepository
                    .FindSingleAsync(x =>
                        x.Token == token);

            if (invitation is null)
            {
                return AuthErrors.InvitationNotFound;

            }

            // Expired

            if (invitation.ExpiresAt
                < DateTime.UtcNow)
            {
                return CompanyErrors.InvitationExpired;

            }

            // Already Accepted

            if (invitation.IsAccepted)
            {
                return Result.Failure(
                    CompanyErrors.InvitationAlreadyAccepted);
            }

            // Current User

            var userResult =
                await _identityService
                    .FindByIdAsync(userId);

            if (userResult.IsFailure)
            {
                return AuthErrors.UserNotFound;

            }

            var user = userResult.Value;

            // Security Check

            if (user.Email!.ToLower()
                != invitation.Email.ToLower())
            {
                return AuthErrors.InvitationNotYours;

            }
            // Assign Company

            if (user.CompanyId.HasValue)
            {
                return Result.Failure(CompanyErrors.EmployeeAlreadyInCompany);
            }

            user.CompanyId =
                invitation.CompanyId;

            // Accept Invitation

            invitation.IsAccepted = true;

            return Result.Success();
        }
    }
}
