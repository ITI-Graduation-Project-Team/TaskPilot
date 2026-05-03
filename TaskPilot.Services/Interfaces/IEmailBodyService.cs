namespace TaskPilot.Services.Interfaces
{
    public interface IEmailBodyService
    {
        string GenerateConfirmationEmailBody(string name, string email, string otp);
        string GeneratePasswordResetEmailBody(string name, string email, string otp);
    }
}
