using TaskPilot.DTOs;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IEmailService
    {
        Task<Result> SendEmailAsync(EmailRequest emailRequest);
    }
}
