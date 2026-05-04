using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using System.Text.Json;
using System.Linq;
using TaskPilot.Services.Interfaces;

public class OpenAiCvParserService : ICvParserService
{
    private readonly ChatClient _client;

    public OpenAiCvParserService(IConfiguration config)
    {
        var apiKey = config["OpenAI:ApiKey"];
        _client = new ChatClient(model: "gpt-4o-mini", apiKey: apiKey);
    }

    public async Task<List<string>> ExtractSkillsAsync(string text)
    {
        var prompt = $"""
        Extract technical skills from this CV.
        Return ONLY a JSON array.

        Example:
        ["C#", "SQL", "React"]

        CV:
        {text}
        """;

        var response = await _client.CompleteChatAsync(
             new ChatMessage[]
             {
        new UserChatMessage(prompt)
              }
        );
        var completion = response.Value;
        var content = completion.Content[0].Text;
        content = CleanJson(content);

        try
        {
            var skills = JsonSerializer.Deserialize<List<string>>(content);
            return skills ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private string CleanJson(string input)
    {
        return input.Replace("```json", "")
                    .Replace("```", "")
                    .Trim();
    }
}