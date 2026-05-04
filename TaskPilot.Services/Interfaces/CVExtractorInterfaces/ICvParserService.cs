namespace TaskPilot.Services.Interfaces
{
    public interface ICvParserService
    {
        Task<List<string>> ExtractSkillsAsync(string text);
    }
}