using TaskPilot.DTOs.Auth;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces.External
{
    public interface IGoogleAuthService
    {
        Task<Result<GoogleUserInfo>> ValidateTokenAsync(string idToken);
    }
}
