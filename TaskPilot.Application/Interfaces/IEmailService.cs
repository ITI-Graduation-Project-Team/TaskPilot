using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Application.Common.Results;

namespace TaskPilot.Application.Interfaces
{
    public interface IEmailService
    {
        Task<Result>SendEmailAsync(string to, string subject, string body);
    }
}
