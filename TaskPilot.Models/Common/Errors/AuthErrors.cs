using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Common.Errors
{
    public class AuthErrors
    {
        public static readonly Error EmailAlreadyRegistered =
          new("EMAIL_ALREADY_REGISTERED", ErrorType.Conflict);

        public static readonly Error EmailAlreadyVerified =
            new("EMAIL_ALREADY_VERIFIED", ErrorType.Conflict);

        public static readonly Error EmailNotConfirmed =
            new("EMAIL_NOT_CONFIRMED", ErrorType.Unauthorized);

        public static readonly Error UserNotFound =
            new("USER_NOT_FOUND", ErrorType.NotFound);

        public static readonly Error InvitationNotFound =
            new("INVITATION_NOT_FOUND", ErrorType.NotFound);

        public static readonly Error InvitationNotYours =
            new("INVITATION_NOT_YOURS", ErrorType.Forbidden);

        public static readonly Error OtpGenerationFailed =
            new("OTP_GENERATION_FAILED", ErrorType.Failure);

        public static readonly Error PasswordResetFailed =
            new("PASSWORD_RESET_FAILED", ErrorType.Failure);

        //refresh token
        public static readonly Error InvalidRefreshToken =
       new("INVALID_REFRESH_TOKEN", ErrorType.Unauthorized);

        public static readonly Error TokenReuseDetected =
            new("TOKEN_REUSE_DETECTED", ErrorType.Unauthorized);

        public static readonly Error SessionExpired =
            new("SESSION_EXPIRED", ErrorType.Unauthorized);

        public static readonly Error SessionExpiredInactive =
            new("SESSION_EXPIRED_INACTIVE", ErrorType.Unauthorized);

        public static readonly Error RefreshTokenGenerationFailed =
            new("REFRESH_TOKEN_GENERATION_FAILED", ErrorType.Failure);

        //google 
        public static readonly Error EmptyGoogleToken =
     new("EMPTY_GOOGLE_TOKEN", ErrorType.Validation);

        public static readonly Error InvalidGoogleToken =
            new("INVALID_GOOGLE_TOKEN", ErrorType.Unauthorized);

        public static readonly Error GoogleAuthenticationFailed =
            new("GOOGLE_AUTHENTICATION_FAILED", ErrorType.Failure);
    }
}
