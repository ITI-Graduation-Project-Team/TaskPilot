using TaskPilot.DTOs.Auth;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Interfaces
{
    public interface IAuthService
    {
        Task<Result> RegisterAsync(RegisterDTO RegisterRequest, UserRole Role);
        Task<Result> ResendConfirmationEmailAsync(string email);
        Task<Result<AuthResponseDTO>> ConfirmEmailAsync(ConfirmEmailDTO confirmEmailDTO);
        Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO loginDTO);
        Task<Result<AuthResponseDTO>>GoogleLoginAsync(string idToken);
        Task<Result> LogoutAsync(string Token);
        Task<Result<AuthResponseDTO>> RefreshTokenAsync(RefreshTokenDTO refreshTokenDto);
        Task<Result> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<Result> ResetPasswordAsync(ResetPasswordDTO resetPasswordDTO);
        Task<Result<InvitationInfoResponse>>
      GetInvitationInfoAsync(
          string token);

        Task<Result>
            CompleteInvitationAsync(
                string token,
                Guid userId);




    }
}
