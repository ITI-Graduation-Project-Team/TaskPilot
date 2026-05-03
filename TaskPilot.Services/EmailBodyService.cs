using TaskPilot.Services.Interfaces;

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
}