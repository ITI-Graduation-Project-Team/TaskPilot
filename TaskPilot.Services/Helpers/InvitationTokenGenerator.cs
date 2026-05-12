using System.Security.Cryptography;

namespace TaskPilot.Services.Helpers
{
    public static class InvitationTokenGenerator
    {
        public static string Generate()
        {
            var bytes = RandomNumberGenerator
                .GetBytes(32);

            return Convert.ToHexString(bytes);
        }
    }
}
