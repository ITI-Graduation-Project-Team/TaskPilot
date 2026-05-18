using System.Text.RegularExpressions;

namespace TaskPilot.AI.Helpers
{
    public static class JsonCleaner
    {
        public static string Clean(
            string input)
        {
            input = input
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var match = Regex.Match(
                input,
                @"\{[\s\S]*\}",
                RegexOptions.Multiline);

            return match.Success
                ? match.Value
                : input;
        }
    }
}