using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Application.Common.Results;

namespace TaskPilot.Application.Interfaces
{
    public interface IEmailBodyService
    {
        string GenerateConfirmationEmailBody(string name ,string email, string otp);
    }
}
