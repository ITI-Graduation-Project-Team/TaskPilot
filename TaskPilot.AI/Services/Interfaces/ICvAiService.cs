using TaskPilot.DTOs.CV;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface ICvAiService
    {
        Task<ParsedCvDto>
            ParseCvAsync(
                string text);
    }
}