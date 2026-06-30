using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Auth;
using TaskPilot.Models.Common;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    public class AuthController : ApiControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IUnitOfWork _unitOfWork;
        public AuthController(IAuthService authService, IUnitOfWork unitOfWork)
        {
            _authService = authService;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register(
              [FromBody] RegisterDTO request,
              CancellationToken cancellationToken,
              UserRole Role)
        {
            var result = await _authService.RegisterAsync(request, Role);
            return HandleResult(result,SuccessCodes.Auth.Register);
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(
            [FromBody] LoginDTO request)
        {
            var result = await _authService.LoginAsync(request);
            return HandleResult(result, SuccessCodes.Auth.Login);
        }

        [HttpPost("confirm-email")]
        public async Task<ActionResult> ConfirmEmail(
            [FromBody] ConfirmEmailDTO request)
        {
            var result = await _authService.ConfirmEmailAsync(request);
            return HandleResult(result, SuccessCodes.Auth.EmailConfirmed);
        }

        [HttpPost("resend-confirmation")]
        public async Task<ActionResult> ResendConfirmation(
            [FromBody] ResendConfirmationDTO request)
        {
            var result = await _authService.ResendConfirmationEmailAsync(request.Email);
            return HandleResult(result, SuccessCodes.Auth.ResendConfirmation);
        }

        [HttpPost("google")]
        public async Task<ActionResult> Google(
            [FromBody] GoogleAuthDTO request)
        {
            var result = await _authService.GoogleLoginAsync(request.IdToken);
            return HandleResult(result, SuccessCodes.Auth.GoogleLogin);
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult> RefreshToken([FromBody] RefreshTokenDTO request)
        {
            var result = await _authService.RefreshTokenAsync(request);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result,SuccessCodes.Auth.TokenRefreshed);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RevokeTokenDTO request)
        {
            var result = await _authService.LogoutAsync(request.RefreshToken);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result,SuccessCodes.Auth.Logout);
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var result = await _authService.ForgotPasswordAsync(dto);
            return HandleResult(result, SuccessCodes.Auth.OtpSent);
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDTO request)
        {
            var result = await _authService.ResetPasswordAsync(request);
            return HandleResult(result, SuccessCodes.Auth.PasswordReset);
        }

        [HttpGet("invitation/{token}")]
        public async Task<ActionResult>
    GetInvitationInfo(
        string token)
        {
            var result =
                await _authService
                    .GetInvitationInfoAsync(token);

            return HandleResult(result);
        }
        [Authorize]
        [HttpPost("complete-invitation")]
        public async Task<ActionResult>
    CompleteInvitation(
        CompleteInvitationDTO request)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(
                    userId,
                    out Guid currentUserId))
            {
                return HandleResult(Result.Failure(CommonErrors.Unauthorized()));
            }

            var result =
                await _authService
                    .CompleteInvitationAsync(
                        request.Token,
                        currentUserId);

            if (result.IsSuccess)
            {
                await _unitOfWork
                    .SaveChangesAsync();
            }

            return HandleResult(result,SuccessCodes.Auth.InvitationCompleted);
        }
    }

}
