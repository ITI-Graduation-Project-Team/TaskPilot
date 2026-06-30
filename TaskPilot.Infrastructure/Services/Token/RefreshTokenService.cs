using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using TaskPilot.Data.Repositories;
using TaskPilot.Infrastructure.Settings;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Token
{
    public class RefreshTokenService:IRefreshTokenService
    {
        private readonly IRepository<RefreshToken> _refreshTokenRepository;
        private readonly RefreshTokenSettings _settings;

        public RefreshTokenService(IOptions<RefreshTokenSettings> settings, IRepository<RefreshToken> refreshTokenRepository)
        {
            _settings = settings.Value;
            _refreshTokenRepository = refreshTokenRepository;
        }


        public async Task<Result<string>> GenerateAsync(User user)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var hashedToken = HashToken(rawToken);
            var token = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = hashedToken,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_settings.ExpiryDays),
                LastActivityAt = DateTime.UtcNow,
                RevokedAt = null
            };
            await _refreshTokenRepository.AddAsync(token);
            return Result.Success(rawToken);

        }
        public async Task<Result<User>> ValidateAsync(string token)
        {
            var hashedToken = HashToken(token);
            RefreshToken? refreshToken = await _refreshTokenRepository.FindSingleAsync(rt => rt.Token == hashedToken, includes: rt => rt.User);
            if (refreshToken is null)
                return CommonErrors.InvalidRefreshToken();

            if (refreshToken.IsRevoked)
            {
                await RevokeAllAsync(refreshToken.UserId);
        
            return AuthErrors.TokenReuseDetected;
            }

            if (refreshToken.IsInactive)
                return AuthErrors.SessionExpiredInactive;

            if (refreshToken.IsExpired)
                return AuthErrors.SessionExpired;

            // rotation — revoke old token
            refreshToken.RevokedAt = DateTime.UtcNow;
            return Result.Success(refreshToken.User);
        }

        public async Task<Result> RevokeAsync(string token)
        {
            var hashedToken = HashToken(token);
            RefreshToken? refreshToken = await _refreshTokenRepository.FindSingleAsync(rt => rt.Token == hashedToken, includes: rt => rt.User);
            if (refreshToken is null)
                return CommonErrors.InvalidRefreshToken();

            if (refreshToken.IsRevoked) return AuthErrors.InvalidRefreshToken;

            refreshToken.RevokedAt = DateTime.UtcNow;
            return Result.Success();
        }

        public async Task RevokeAllAsync(Guid userId)
        {
            var tokens = await _refreshTokenRepository.FindAsync(rt => rt.UserId == userId && !rt.IsRevoked);
            foreach (var token in tokens)
                token.RevokedAt = DateTime.UtcNow;

        }
        private static string HashToken(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToBase64String(bytes);
        }
    }
}
