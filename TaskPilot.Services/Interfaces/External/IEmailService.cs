using TaskPilot.DTOs;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces.External
{
    public interface IEmailService
    {
        Task<Result> SendEmailAsync(EmailRequest emailRequest);
    }
}
