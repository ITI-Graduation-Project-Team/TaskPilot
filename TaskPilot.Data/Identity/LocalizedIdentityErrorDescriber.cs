using Microsoft.AspNetCore.Identity;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Data.Identity
{
    public class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
    {
        private readonly ILocalizationService _localizer;

        public LocalizedIdentityErrorDescriber(ILocalizationService localizer)
        {
            _localizer = localizer;
        }

        public override IdentityError DuplicateEmail(string email)
        {
            return new IdentityError
            {
                Code = nameof(DuplicateEmail),
                Description = _localizer.GetString("EMAIL_ALREADY_REGISTERED")
            };
        }

        public override IdentityError PasswordRequiresNonAlphanumeric()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresNonAlphanumeric),
                Description = _localizer.GetString("PASS_REQ_NON_ALPHANUMERIC")
            };
        }

        public override IdentityError PasswordRequiresDigit()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresDigit),
                Description = _localizer.GetString("PASS_REQ_DIGIT")
            };
        }

        public override IdentityError PasswordRequiresLower()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresLower),
                Description = _localizer.GetString("PASS_REQ_LOWER")
            };
        }

        public override IdentityError PasswordRequiresUpper()
        {
            return new IdentityError
            {
                Code = nameof(PasswordRequiresUpper),
                Description = _localizer.GetString("PASS_REQ_UPPER")
            };
        }

        public override IdentityError PasswordTooShort(int length)
        {
            var format = _localizer.GetString("PASS_TOO_SHORT");
            return new IdentityError
            {
                Code = nameof(PasswordTooShort),
                Description = string.Format(format, length)
            };
        }

        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        {
            var format = _localizer.GetString("PASS_REQ_UNIQUE_CHARS");
            return new IdentityError
            {
                Code = nameof(PasswordRequiresUniqueChars),
                Description = string.Format(format, uniqueChars)
            };
        }

        public override IdentityError InvalidToken()
        {
            return new IdentityError
            {
                Code = nameof(InvalidToken),
                Description = _localizer.GetString("INVALID_TOKEN")
            };
        }
    }
}
