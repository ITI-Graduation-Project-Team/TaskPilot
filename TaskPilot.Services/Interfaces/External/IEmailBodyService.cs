namespace TaskPilot.Services.Interfaces.External
{
    public interface IEmailBodyService
    {
        string GenerateConfirmationEmailBody(string name, string email, string otp);
        string GeneratePasswordResetEmailBody(string name, string email, string otp);
        string GenerateEmployeeInvitationBody(
            string employeeName,
            string companyName,
            string invitationLink);
    }
}
