using System.ComponentModel.DataAnnotations;

namespace TaskPilot.Services.Helpers
{
    public static class EmailValidator
    {
        private static readonly EmailAddressAttribute _emailAttribute = new();

        public static bool IsValid(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
                
            return _emailAttribute.IsValid(email.Trim());
        }
    }
}
