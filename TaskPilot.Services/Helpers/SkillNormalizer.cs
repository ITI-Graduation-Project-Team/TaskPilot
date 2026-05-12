using System.Text.RegularExpressions;

namespace TaskPilot.Services.Helpers
{
        public static class SkillNormalizer
        {
            private static readonly Dictionary<string, string> SpecialCases =
                new()
                {
                { "c#", "csharp" },
                { "c++", "cplusplus" },
                { ".net", "dotnet" },
                { "asp.net", "aspnet" },
                { "asp.net core", "aspnet core" },
                { "node.js", "nodejs" },
                { "react.js", "reactjs" },
                { "next.js", "nextjs" }
                };

            public static string Normalize(string skill)
            {
                if (string.IsNullOrWhiteSpace(skill))
                    return string.Empty;

                skill = skill.Trim().ToLower();

                if (SpecialCases.TryGetValue(skill, out var normalized))
                    return normalized;

                skill = Regex.Replace(skill, @"[^\w\s]", " ");

                skill = Regex.Replace(skill, @"\s+", " ");

                return skill.Trim();
            }
        }
}

