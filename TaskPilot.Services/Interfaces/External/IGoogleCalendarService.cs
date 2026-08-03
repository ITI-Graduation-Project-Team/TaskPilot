using System;
using System.Threading.Tasks;

namespace TaskPilot.Services.Interfaces.External
{
    public interface IGoogleCalendarService
    {
        string GetGoogleLoginUrl(Guid userId);
        Task<bool> ExchangeCodeForTokenAsync(string code, Guid userId);
        Task<string> AddEventToCalendarAsync(Guid userId, string title, string description, DateTime startTime, DateTime endTime);
    }
}

