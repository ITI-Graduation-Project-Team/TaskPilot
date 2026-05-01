using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Application.DTOs;
using TaskPilot.Application.Interfaces;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
        CancellationToken cancellationToken,[FromQuery]UserRole Role)
        {
            var result = await _authService.RegisterAsync(request, Role, cancellationToken);
            return HandleCreated(result, result.Value);
        }
    }
}
