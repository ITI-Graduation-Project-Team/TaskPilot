using System;
using System.Collections.Generic;
using System.Text;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    public interface ITokenService
    {
        Task<string> GenerateAccessToken(User user);
    }

}
