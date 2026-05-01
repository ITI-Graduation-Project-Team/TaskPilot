using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IAuthService
    {
        Task<Result<string>> RegisterAsync(DTOs.RegisterDTO RegisterRequest, Models.Enums.UserRole Role, CancellationToken cancellationToken);
    }
}
