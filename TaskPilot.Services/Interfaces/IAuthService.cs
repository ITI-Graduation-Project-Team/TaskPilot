using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Auth;

namespace TaskPilot.Services.Interfaces
{
    /// <summary>
    /// Abstraction over ASP.NET Identity operations.
    /// Implemented in the Infrastructure layer so the Application layer
    /// never depends on Identity or EF directly.
    /// </summary>
    public interface IAuthService
    {
        Task<Result<AuthResponseDTO>> RegisterAsync(RegisterDTO dto);
        Task<Result<AuthResponseDTO>> LoginAsync(LoginDTO dto);
    }
}
