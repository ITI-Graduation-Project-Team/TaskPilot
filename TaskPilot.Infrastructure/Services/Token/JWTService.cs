using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TaskPilot.Infrastructure.Settings;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Token
{
    public class JWTService : ITokenService
    {
        private readonly IIdentityService _identityService;
        private readonly JWTSettings _jwt;

        public JWTService(IIdentityService identityService, IOptions<JWTSettings> jwt)
        {
            _identityService = identityService;
            _jwt = jwt.Value;
        }

        public async Task<string> GenerateAccessToken(User user)
        {
            var UserClaims = await _identityService.GetClaimsAsync(user);
            var roles = await _identityService.GetRolesAsync(user);
            if (UserClaims.IsFailure)
            {
                throw new Exception(UserClaims.Error.Description);
            }
            var roleClaims = new List<Claim>();

            if (roles.IsSuccess && roles.Value != null)
            {
                foreach (var role in roles.Value)
                {
                    roleClaims.Add(new Claim(ClaimTypes.Role, role));
                    roleClaims.Add(new Claim("role", role));
                }
            }

            var claims = new[]
            {
                 new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            }
            .Union(UserClaims.Value ?? Enumerable.Empty<Claim>())
            .Union(roleClaims).ToList();
            if (user is Employee employee)
            {
                claims.Add(new Claim("ProfileCompleted", employee.IsProfileCompleted.ToString()));
            }
            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            var jwtSecurityToken = new JwtSecurityToken(
             issuer: _jwt.Issuer,
             audience: _jwt.Audience,
             claims: claims,
              notBefore: now,
             expires: now.AddMinutes(_jwt.DurationInMinutes),
             signingCredentials: signingCredentials);


            return new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

        }
    }
}
