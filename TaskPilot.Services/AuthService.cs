using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs;
using TaskPilot.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services
{
    public class AuthService : IAuthService
    {
        private readonly IIdentityService _identityService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IEmailBodyService _emailBodyService;

        public AuthService(IIdentityService identityService, IUnitOfWork unitOfWork, IEmailService emailService, IEmailBodyService emailBodyService)
        {
            _identityService = identityService;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _emailBodyService = emailBodyService;
        }

        public async Task<Result<string>> RegisterAsync(RegisterDTO RegisterRequest, UserRole Role, CancellationToken cancellationToken)
        {
            var ExistingUser = await _identityService.FindByEmailAsync(RegisterRequest.Email);
            if (ExistingUser != null)
            {
                return Result.Failure<string>(CommonErrors.Conflict("This email address is already registered."));
            }
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                var CreatedUser = await _identityService.CreateUserAsync(RegisterRequest.Email, RegisterRequest.Password);
                if (CreatedUser.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return Result.Failure<string>(CreatedUser.Error);
                }
                var AddToRoleResult = await _identityService.AddToRoleAsync(CreatedUser.Value, Role.ToString());
                if (AddToRoleResult.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return Result.Failure<string>(AddToRoleResult.Error);
                }
                var DomainUser = new User
                {
                    ApplicationUserId = CreatedUser.Value,
                    FirstNameAr = RegisterRequest.FirstNameAr,
                    LastNameAr = RegisterRequest.LastNameAr,
                    FirstNameEn = RegisterRequest.FirstNameEn,
                    LastNameEn = RegisterRequest.LastNameEn
                };
                await _unitOfWork.Users.AddAsync(DomainUser);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return Result.Failure<string>(CommonErrors.OperationFailed());
            }
            var OtpResult = await _identityService.GenerateOTPAsync(RegisterRequest.Email);
            if (OtpResult.IsFailure)
            {
                return Result.Failure<string>(CommonErrors.OperationFailed("Account created successfully, but failed to generate OTP. Please try resending the code."));
            }
            var name = RegisterRequest.FirstNameEn + " " + RegisterRequest.LastNameEn;
            var EmailBody =
             _emailBodyService.GenerateConfirmationEmailBody(name, RegisterRequest.Email, OtpResult.Value);
            var EmailResult = await _emailService.SendEmailAsync(RegisterRequest.Email, "Email Confirmation", EmailBody);
            if (EmailResult.IsFailure)
            {
                return Result.Failure<string>(CommonErrors.OperationFailed("Account created successfully, but we couldn't send the confirmation email. Please try resending it."));
            }
            return Result.Success("Registered successfully. Please check your email to verify.");
        }
    }
}
