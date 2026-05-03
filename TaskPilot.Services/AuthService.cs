using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.DTOs.Auth;
using TaskPilot.DTOs;

namespace TaskPilot.Services
{
    public class AuthService : IAuthService
    {
        private readonly IIdentityService _identityService;
        private readonly IEmailService _emailService;
        private readonly IEmailBodyService _emailBodyService;
        private readonly ITokenService _tokenService;
        private readonly IGoogleAuthService _googleAuthService;

        public AuthService(IIdentityService identityService, IEmailService emailService, IEmailBodyService emailBodyService, ITokenService tokenService, IGoogleAuthService googleAuthService)
        {
            _identityService = identityService;
            _emailService = emailService;
            _emailBodyService = emailBodyService;
            _tokenService = tokenService;
            _googleAuthService = googleAuthService;
        }

        public async Task<Result<string>> RegisterAsync(RegisterDTO RegisterRequest, UserRole Role)
        {

            //1 check 
            var existingUser = await _identityService.FindByEmailAsync(RegisterRequest.Email);
            if (existingUser.IsSuccess)
            {
                return CommonErrors.Conflict("This email address is already registered.");
            }
            //2 create
            var user = new User
            {

                Email = RegisterRequest.Email,
                FirstNameAr = RegisterRequest.FirstNameAr,
                LastNameAr = RegisterRequest.LastNameAr,
                FirstNameEn = RegisterRequest.FirstNameEn,
                LastNameEn = RegisterRequest.LastNameEn,
                UserName = RegisterRequest.Email,
            };
            var CreatedUser = await _identityService.CreateUserAsync(user, RegisterRequest.Password);
            if (CreatedUser.IsFailure)
            {
                return CreatedUser.Error;
            }
            //3 add to role
            var AddToRoleResult = await _identityService.AddToRoleAsync(CreatedUser.Value, Role.ToString());
            if (AddToRoleResult.IsFailure)
            {
                await _identityService.DeleteUserAsync(CreatedUser.Value);
                return AddToRoleResult.Error;
            }
            // send confirmation email
            return await SendConfirmationEmailAsync(CreatedUser.Value);
        }
        public async Task<Result<string>> ResendConfirmationEmailAsync(string email)
        {
            var userResult = await _identityService.FindByEmailAsync(email);
            if (userResult.IsFailure)
            {
                return Result.Success("If the email is registered, a new code will be sent.");
            }
            var user = userResult.Value;

            if (user.EmailConfirmed)
            {
                return CommonErrors.Conflict("This email is already verified. Please log in.");
            }

            return await SendConfirmationEmailAsync(user);
        }
        public async Task<Result<AuthResponseDTO>> ConfirmEmailAsync(ConfirmEmailDTO confirmEmailDTO)
        {
            //1
            var userResult = await _identityService.FindByEmailAsync(confirmEmailDTO.Email);
            if (userResult.IsFailure)
            {
                return CommonErrors.NotFound("user");
            }
            //2
            var user = userResult.Value;
            var verifyResult = await _identityService.VerifyEmailAsync(user, confirmEmailDTO.OTP);

            if (verifyResult.IsFailure)
            {
                return verifyResult.Error;
            }

            var token = await _tokenService.GenerateAccessToken(userResult.Value);
            var response = new AuthResponseDTO
            {
                Email = confirmEmailDTO.Email,
                Token = token,
                UserId = userResult.Value.Id,
                Message = "Email confirmed successfully."
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
                return CommonErrors.Conflict("Email is not confirmed. Please confirm your email before logging in.");
            }
            var passwordCheckResult = await _identityService.CheckPasswordAsync(user, loginDTO.Password);
            if (passwordCheckResult.Value == false)
            {
                return CommonErrors.Unauthorized("Invalid credentials.");
            }
            var token = await _tokenService.GenerateAccessToken(user);
            var response = new AuthResponseDTO
            {
                Email = user.Email,
                Token = token,
                UserId = user.Id,
                Message = "Login successful."
            };
            return response;
        }


        private async Task<Result<string>> SendConfirmationEmailAsync(User user)
        {
            //1
            if (user.EmailConfirmed)
            {
                return CommonErrors.Conflict("This email is already verified.");
            }
            //2
            var OtpResult = await _identityService.GenerateOTPAsync(user);
            if (OtpResult.IsFailure)
            {
                return CommonErrors.OperationFailed("Failed to generate OTP");
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
            return Result.Success("OTP sent successfully.");
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
                return userResult.Error;
            }
            var user = userResult.Value;
            //3-generate our token
            var token = await _tokenService.GenerateAccessToken(user);
            var response = new AuthResponseDTO
            {
                Email = user.Email,
                Token = token,
                UserId = user.Id,
                Message = "Login successful."
            };
            return response;

        }

        public async Task<Result<string>> ForgotPasswordAsync(string email)
        {
            var userResult = await _identityService.FindByEmailAsync(email);
            if (userResult.IsFailure)
            {
                return Result.Success("If the email is registered, a password reset link will be sent.");
            }
            var user = userResult.Value;
            var resetTokenResult = await _identityService.GeneratePasswordResetTokenAsync(user);
            if (resetTokenResult.IsFailure)
            {
                return CommonErrors.OperationFailed("Failed to generate password reset token.");
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
            return Result.Success("If the email is registered, a password reset link will be sent.");

        }
        public async Task<Result<string>> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO)
        {
          
            var userResult = await _identityService.FindByEmailAsync(resetPasswordDTO.Email);
            if (userResult.IsFailure)
            {
                return CommonErrors.NotFound("user");
            }
            var user = userResult.Value;
            var resetResult = await _identityService.ResetPasswordAsync(user, resetPasswordDTO.OTP, resetPasswordDTO.Password);
            if (resetResult.IsFailure)
            {
                return resetResult.Error;
            }
            return Result.Success("Password has been reset successfully.");
        }
    }
}