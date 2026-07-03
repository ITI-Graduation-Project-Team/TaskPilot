using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Common.Errors
{
    public class IdentityErrors
    {
        public static readonly Error UserNotFound =
            new("USER_NOT_FOUND", ErrorType.NotFound);

        public static readonly Error RoleCreationFailed =
            new("ROLE_CREATION_FAILED", ErrorType.Failure);

        public static readonly Error UserCreationFailed =
            new("USER_CREATION_FAILED", ErrorType.Failure);

        public static readonly Error UserDeletionFailed =
            new("USER_DELETION_FAILED", ErrorType.Failure);

        public static readonly Error RoleAssignmentFailed =
            new("ROLE_ASSIGNMENT_FAILED", ErrorType.Failure);

        public static readonly Error EmailVerificationFailed =
            new("EMAIL_VERIFICATION_FAILED", ErrorType.Failure);

        public static readonly Error AccountLocked =
            new("ACCOUNT_LOCKED", ErrorType.Unauthorized);

        public static readonly Error ExternalLoginFailed =
            new("EXTERNAL_LOGIN_FAILED", ErrorType.Failure);

        public static readonly Error PasswordResetFailed =
            new("PASSWORD_RESET_FAILED", ErrorType.Failure);

        public static Error PasswordResetValidationFailed(string errors) =>
            new("PASSWORD_RESET_VALIDATION_FAILED", ErrorType.Validation, errors);
    }
}
