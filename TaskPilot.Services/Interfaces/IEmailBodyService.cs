namespace TaskPilot.Services.Interfaces
{
    public interface IEmailBodyService
    {
        string GenerateConfirmationEmailBody(string name, string email, string otp);
    }
}
