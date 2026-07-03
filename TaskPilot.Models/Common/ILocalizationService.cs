namespace TaskPilot.Models.Common
{
    public interface ILocalizationService
    {
        string GetString(string key);
        string CurrentLanguage { get; }
    }
}
