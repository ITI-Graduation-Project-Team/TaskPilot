using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TaskPilot.DTOs.Auth;
using TaskPilot.Infrastructure.Settings;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Google
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly GoogleSettings _googleSettings;
        public GoogleAuthService(IOptions<GoogleSettings> googleSettings)
        {
            _googleSettings = googleSettings.Value;
        }
        public async Task<Result<GoogleUserInfo>> ValidateTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return CommonErrors.InvalidInput("Google token cannot be empty.");
            }
            try
            {

                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new List<string>
                {
                    _googleSettings.ClientId
                }
                };
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

                return new GoogleUserInfo
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName
                };
            }
            catch (InvalidJwtException)
            {
                return CommonErrors.Unauthorized("Invalid Google token.");
            }
            catch (Exception ex)
            {
                return CommonErrors.ServerError();
            }

        }
    }
}

