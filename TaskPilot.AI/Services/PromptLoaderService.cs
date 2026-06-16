using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services
{
    public class PromptLoaderService
        : IPromptLoaderService
    {
        private readonly string _promptsBasePath;

        public PromptLoaderService()
        {
            // Points to TaskPilot.AI bin folder
            // where YAML files get copied
            _promptsBasePath = Path.Combine(
                AppContext.BaseDirectory,
                "Prompts");
        }

        public async Task<string>
            LoadAsync(
                string relativePath)
        {
            var fullPath =
                Path.Combine(
                    _promptsBasePath,
                    relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Prompt file not found: {fullPath}");
            }

            return await File
                .ReadAllTextAsync(fullPath);
        }
    }
}