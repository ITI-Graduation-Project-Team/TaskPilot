using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs;
using TaskPilot.DTOs.Auth;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    public class AuthController : ApiControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register(
            [FromBody] RegisterDTO request,
            CancellationToken cancellationToken,
             UserRole Role)
        {
            var result = await _authService.RegisterAsync(request, Role);

            // ❌ الخطأ كان هنا: كنت تحاول عمل return Ok(result.Value) مباشرة

            // ✅ الحل الصحيح: افحص النجاح أولاً
            if (result.IsFailure)
            {
                // يمكنك استخدام Helper method لتحويل الـ Error إلى StatusCode مناسب (400, 404, 409...)
                return BadRequest(result.Error);
            }

            // الآن الوصول لـ Value آمن 100%
            return Ok(result.Value);
            //var result = await _authService.RegisterAsync(request, Role);
            //return HandleCreated(result, result.Value);
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
    }
}
