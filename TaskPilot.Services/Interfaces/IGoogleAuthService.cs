using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.DTOs;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IGoogleAuthService
    {
        Task<Result<GoogleUserInfo>> ValidateTokenAsync(string idToken);
    }
}
