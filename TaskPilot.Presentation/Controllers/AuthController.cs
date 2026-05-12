using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs;
using TaskPilot.DTOs.Auth;
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
            return HandleResult(result, "Registered successfully.");
        }
        [HttpPost("login")]
        public async Task<ActionResult> Login(
         [FromBody] LoginDTO request
            )
        {
            var result = await _authService.LoginAsync(request);
            return HandleResult(result);
        }
        [HttpPost("confirm-email")]
        public async Task<ActionResult> ConfirmEmail(
          [FromBody] ConfirmEmailDTO request
          )
        {
            var result = await _authService.ConfirmEmailAsync(request);
            return HandleResult(result);
        }
        [HttpPost("resend-confirmation")]
        public async Task<ActionResult> ResendConfirmation(
           [FromBody] ResendConfirmationDTO request
         )
        {
            var result = await _authService.ResendConfirmationEmailAsync(request.Email);
            return HandleResult(result);
        }
        [HttpPost("google")]
        public async Task<ActionResult> Google(
    [FromBody] GoogleAuthDTO request
     )
        {
            var result = await _authService
                .GoogleLoginAsync(request.IdToken);

            return HandleResult(result);
        }
        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody]string email)
        {
            var result=await _authService.ForgotPasswordAsync(email);
            return HandleResult(result);
        }
        [HttpPost("reset-password")]
        public async Task<ActionResult>ResetPassword([FromBody]ResetPasswordDTO request)
        {
            var result=await _authService.ResetPasswordAsync(request);
            return HandleResult(result);
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
                return Unauthorized();
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

            return HandleResult(result);
        }
    }

}
