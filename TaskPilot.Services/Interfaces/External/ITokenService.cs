using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces.External
{
    public interface ITokenService
    {
        Task<string> GenerateAccessToken(User user);
    }

}
