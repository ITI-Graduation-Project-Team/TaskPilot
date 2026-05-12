using TaskPilot.DTOs;
using TaskPilot.DTOs.Auth;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Interfaces
{
    public interface IAuthService
    {
        Task<Result> RegisterAsync(DTOs.RegisterDTO RegisterRequest, UserRole Role);
        Task<Result<string>> ResendConfirmationEmailAsync(string email);
        Task<Result<AuthResponseDTO>> ConfirmEmailAsync(ConfirmEmailDTO confirmEmailDTO);
        Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO loginDTO);
        Task<Result<AuthResponseDTO>>GoogleLoginAsync(string idToken);
        Task<Result<string>> ForgotPasswordAsync(string email);
        Task<Result<string>> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO);





    }
}
