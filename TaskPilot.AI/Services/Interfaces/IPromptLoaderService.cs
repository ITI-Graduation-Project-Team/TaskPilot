namespace TaskPilot.AI.Services.Interfaces
{
    public interface IPromptLoaderService
    {
        Task<string> LoadAsync(
            string relativePath);
    }
}