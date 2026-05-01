using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IEmailService
    {
        Task<Result> SendEmailAsync(string to, string subject, string body);
    }
}
