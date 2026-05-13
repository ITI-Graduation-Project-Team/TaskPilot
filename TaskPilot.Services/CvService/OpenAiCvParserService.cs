using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TaskPilot.DTOs.CV;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class OpenAiCvParserService : ICvParserService
    {
        private readonly ChatClient _client;

        public OpenAiCvParserService(IConfiguration config)
        {
            var apiKey = config["OpenAI:ApiKey"];

            _client = new ChatClient(
                model: "gpt-4o-mini",
                apiKey: apiKey);
        }

        private static JsonSerializerOptions SerializerOptions =>
            new()
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };

        public async Task<ParsedCvDto> ParseCvAsync(string text)
        {
                var prompt = $$"""
                Analyze this CV and return ONLY valid JSON.

                Extract:
                - Job Title
                - Seniority Level
                - Total Years Of Experience
                - Technical Skills
                - Skill Level
                - Years of experience per skill
                - Confidence score for each skill from 0 to 1

                Rules:
                - Return ONLY technical skills.
                - Do NOT include soft skills.
                - Do NOT include explanations.
                - Do NOT wrap JSON in markdown.
                - Return ONLY JSON.

                SeniorityLevel must be one of:
                - Junior
                - MidLevel
                - Senior
                - Lead

                Skill level must be one of:
                - Beginner
                - Intermediate
                - Advanced
                - Expert

                JSON format:

                {
                  "jobTitle": "",
                  "seniorityLevel": "",
                  "totalYearsOfExperience": 0,
                  "skills": [
                    {
                      "name": "",
                      "level": "",
                      "yearsOfExperience": 0,
                      "confidenceScore": 0.0
                    }
                  ]
                }

                CV:
                {{text}}
                """;

            var response = await _client.CompleteChatAsync(
                new ChatMessage[]
                 {
        new UserChatMessage(prompt)
                  },
            new ChatCompletionOptions
            {
                MaxOutputTokenCount = 2000,
                Temperature = 0.2f
            });

            var completion = response.Value;

            var content = completion.Content[0].Text;

            content = CleanJson(content);

            try
            {
                var result = JsonSerializer.Deserialize<ParsedCvDto>(
                    content,
                    SerializerOptions);

                return result ?? new ParsedCvDto();
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Failed to parse CV response.",
                    ex);
            }
        }

        private string CleanJson(string input)
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