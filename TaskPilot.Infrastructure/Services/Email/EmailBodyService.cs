using TaskPilot.Services.Interfaces.External;

namespace TaskPilot.Infrastructure.Services.Email
{
    public class EmailBodyService : IEmailBodyService
    {
        public string GenerateConfirmationEmailBody(string name, string email, string otp)
        {
            return $@"
            <h2>Hello {name}</h2>
            <p>Your confirmation code is:</p>
            <h3>{otp}</h3>
            <p>This code will expire soon.</p>
        ";
        }
        public string GeneratePasswordResetEmailBody(string name, string email, string otp)
        {
            return $@"
            <h2>Hello {name}</h2>
            <p>Your password reset code is:</p>
            <h3>{otp}</h3>
            <p>This code will expire soon.</p>
        ";
        }
        public string GenerateEmployeeInvitationBody(
        string employeeName,
        string companyName,
        string invitationLink)
        {
            return $@"
        <h2>Welcome to {companyName}</h2>

        <p>Hello {employeeName},</p>

        <p>
            You have been invited to join
            <strong>{companyName}</strong>
            on TaskPilot.
        </p>

        <p>
            Click the button below to
            complete your registration:
        </p>

        <a href='{invitationLink}'
           style='padding:12px 20px;
                  background:#2563eb;
                  color:white;
                  text-decoration:none;
                  border-radius:6px;'>
            Accept Invitation
        </a>

        <p>
            This invitation expires in 7 days.
        </p>";
        }
    }
}