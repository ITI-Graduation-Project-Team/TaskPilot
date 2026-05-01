using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs;
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
            [FromQuery] UserRole Role)
        {
            var result = await _authService.RegisterAsync(request, Role, cancellationToken);
            return HandleCreated(result, result.Value);
        }
    }
}
