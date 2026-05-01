using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Application.Common.Results;
using TaskPilot.Application.DTOs;
using TaskPilot.Domain.Enums;

namespace TaskPilot.Application.Interfaces
{
    public interface IAuthService
    {
        Task<Result<string>> RegisterAsync(RegisterDTO RegisterRequest, UserRole Role, CancellationToken cancellationToken);
    }
}
