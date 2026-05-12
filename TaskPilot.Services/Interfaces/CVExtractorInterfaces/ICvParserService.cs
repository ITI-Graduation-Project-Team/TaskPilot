using TaskPilot.DTOs.CV;

namespace TaskPilot.Services.Interfaces
{
    public interface ICvParserService
    {
        Task<ParsedCvDto> ParseCvAsync(string text);
    }
}