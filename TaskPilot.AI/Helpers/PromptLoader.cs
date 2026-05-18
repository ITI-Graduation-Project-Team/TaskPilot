namespace TaskPilot.AI.Helpers
{
    public static class PromptLoader
    {
        public static string Load(
            string path)
        {
            var fullPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    path);

            return File.ReadAllText(
                fullPath);
        }
    }
}