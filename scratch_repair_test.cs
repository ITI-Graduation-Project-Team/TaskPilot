using System;
using System.Text.Json;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Planning
{
    public sealed class SuggestedStoryDto
    {
        public Guid StoryId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public decimal EstimatedHours { get; set; }
        public int PriorityScore { get; set; }
        public string ReasonEn { get; set; } = string.Empty;
        public string ReasonAr { get; set; } = string.Empty;
    }

    public sealed class SprintSuggestionDto
    {
        public int SprintNumber { get; set; } = 1;
        public string SprintTitleEn { get; set; } = string.Empty;
        public string SprintTitleAr { get; set; } = string.Empty;
        public string SprintGoalEn { get; set; } = string.Empty;
        public string SprintGoalAr { get; set; } = string.Empty;
        public decimal TotalEstimatedHours { get; set; }
        public List<string> Risks { get; set; } = new();
        public List<SuggestedStoryDto> Stories { get; set; } = new();
    }
}

public class Program 
{
    public static void Main() 
    {
        string rawJson = @"{
  ""sprintNumber"": 1,
  ""sprintTitleEn"": ""Sprint 1: Core Features"",
  ""sprintTitleAr"": ""سبرينت 1"",
  ""sprintGoalEn"": ""Deliver core functionality."",
  ""sprintGoalAr"": ""تسليم الوظائف الأساسية"",
  ""totalEstimatedHours"": 40,
  ""risks"": [""Risk 1""],
  ""stories"": [
    {
      ""storyId"": ""a0000000-0000-0000-0000-000000000000"",
      ""titleEn"": ""Story 1"",
      ""titleAr"": ""قصة 1"",
      ""estimatedHours"": 10,
      ""priorityScore"": 100,
      ""reasonEn"": ""Because."",
      ""reasonAr"": ""لأن.""
    },
    {
      ""storyId"": ""b0000000-0000-0000-0000-000000000000"",
      ""titleEn"": ""Story 2"",
      ""titleAr"": ""قصة 2"",
      ""estimatedHours"": 5,
      ""priorityScore"": 90,
      ""reasonEn"": ""Because."",
      ""reasonAr"": ""لأن."""; // TRUNCATED MID-STORY 2

        var repaired = TryRepairJson(rawJson);
        Console.WriteLine("Repaired JSON:\n" + repaired);

        var dto = JsonSerializer.Deserialize<TaskPilot.DTOs.Planning.SprintSuggestionDto>(repaired, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Console.WriteLine("" + (dto != null && dto.Stories.Count == 1 ? "SUCCESS" : "FAILED"));
    }

    private static string TryRepairJson(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith(""```json""))
        {
            raw = raw.Substring(7);
            if (raw.EndsWith(""```""))
            {
                raw = raw.Substring(0, raw.Length - 3);
            }
            raw = raw.Trim();
        }
        else if (raw.StartsWith(""```""))
        {
            raw = raw.Substring(3);
            if (raw.EndsWith(""```""))
            {
                raw = raw.Substring(0, raw.Length - 3);
            }
            raw = raw.Trim();
        }

        if (!raw.StartsWith(""{"")) return raw;
        if (raw.EndsWith(""}"")) return raw;

        int lastClosingBrace = -1;
        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '""') { inString = !inString; continue; }
            if (inString) continue;

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 1) 
                    lastClosingBrace = i;
                else if (depth == 0)
                    return raw; 
            }
        }

        if (lastClosingBrace == -1) return raw;

        return raw.Substring(0, lastClosingBrace + 1) + ""\n  ]\n}"";
    }
}
