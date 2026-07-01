namespace TaskPilot.Services.Interfaces
{
    public interface ILocalizationService
    {
        string GetString(string key);
        //string GetLocalizedProperty(string enValue, string arValue);
        string CurrentLanguage { get; }
    }
}
