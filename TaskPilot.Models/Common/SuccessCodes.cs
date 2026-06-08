using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.Models.Common
{
    public class SuccessCodes
    {
        public static class Auth
        {
            public const string ResendConfirmation = "RESEND_CONFIRMATION_SUCCESS";
            public const string GoogleLogin = "GOOGLE_LOGIN_SUCCESS";
            public const string TokenRefreshed = "TOKEN_REFRESHED_SUCCESS";
            public const string Register = "REGISTER_SUCCESS";
            public const string Login = "LOGIN_SUCCESS";
            public const string EmailConfirmed = "EMAIL_CONFIRMED_SUCCESS";
            public const string OtpSent = "OTP_SENT_SUCCESS";
            public const string ForgotPassword = "PASSWORD_RESET_OTP_SENT";
            public const string PasswordReset = "PASSWORD_RESET_SUCCESS";
            public const string Logout = "LOGOUT_SUCCESS";
            public const string InvitationCompleted = "INVITATION_COMPLETED_SUCCESS";
        }
    }
}
