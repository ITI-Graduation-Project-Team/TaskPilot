using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Auth;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    /// <summary>
    /// Handles authentication endpoints (register, login).
    /// Injects IAuthService for business logic and IUnitOfWork for SaveChanges.
    /// </summary>
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
        public async Task<ActionResult> Register([FromBody] RegisterDTO dto)
        {
            var result = await _authService.RegisterAsync(dto);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, "User registered successfully.");
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDTO dto)
        {
            var result = await _authService.LoginAsync(dto);
            return HandleResult(result);
        }
    }
}
