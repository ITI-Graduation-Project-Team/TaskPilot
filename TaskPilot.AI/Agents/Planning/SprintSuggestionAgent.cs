using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.Planning;

namespace TaskPilot.AI.Agents.Planning
{
    public class SprintSuggestionAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public SprintSuggestionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public virtual async Task<SprintSuggestionDto> SuggestSprintAsync(
            Guid projectId,
            string projectName,
            int sprintDurationInDays,
            decimal targetSprintHours,
            decimal utilizedHours,
            string selectedStoriesJson,
            string excludedStoriesJson,
            string retrospectiveContext = "",
            int sprintNumber = 1,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(
                ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync(
                "Planning/SprintSuggestion.yaml");

            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                MaxTokens = 16384,
                Temperature = 0.0,
                Seed = 42,
                ResponseFormat = "json_object"
            };

            var arguments = new KernelArguments(executionSettings)
            {
                ["projectId"] = projectId.ToString(),
                ["projectName"] = projectName,
                ["sprintDurationInDays"] = sprintDurationInDays.ToString(),
                ["targetSprintHours"] = targetSprintHours.ToString(),
                ["utilizedHours"] = utilizedHours.ToString(),
                ["selectedStories"] = selectedStoriesJson,
                ["excludedStories"] = excludedStoriesJson,
                ["retrospectiveContext"] = retrospectiveContext ?? string.Empty,
                ["sprintNumber"] = sprintNumber.ToString()
            };

            SprintSuggestionDto? suggestion = null;
            int maxAttempts = 3;
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var result = await kernel.InvokeAsync(function, arguments, cancellationToken);
                    var raw = result.ToString();
                    var repairedJson = TryRepairJson(raw);
                    
                    suggestion = JsonSerializer.Deserialize<SprintSuggestionDto>(repairedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (suggestion != null && suggestion.Stories.Any())
                    {
                        break;
                    }
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                }
            }

            if (suggestion == null || !suggestion.Stories.Any())
            {
                throw new InvalidOperationException($"Sprint suggestion failed after 3 attempts due to invalid JSON.", lastException);
            }

            if (suggestion.SprintNumber <= 0)
            {
                suggestion.SprintNumber = sprintNumber;
            }

            if (string.IsNullOrWhiteSpace(suggestion.SprintTitleEn))
            {
                suggestion.SprintTitleEn = $"Sprint {sprintNumber}";
            }

            if (string.IsNullOrWhiteSpace(suggestion.SprintTitleAr))
            {
                suggestion.SprintTitleAr = $"السبرينت {sprintNumber}";
            }

            return suggestion;
        }

        public static string TryRepairJson(string raw)
        {
            raw = raw.Trim();
            if (raw.StartsWith("```json"))
            {
                raw = raw.Substring(7);
                if (raw.EndsWith("```"))
                {
                    raw = raw.Substring(0, raw.Length - 3);
                }
                raw = raw.Trim();
            }
            else if (raw.StartsWith("```"))
            {
                raw = raw.Substring(3);
                if (raw.EndsWith("```"))
                {
                    raw = raw.Substring(0, raw.Length - 3);
                }
                raw = raw.Trim();
            }

            if (!raw.StartsWith("{")) return raw;
            if (raw.EndsWith("}")) return raw;

            int lastClosingBrace = -1;
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (escape) { escape = false; continue; }
                if (c == '\\' && inString) { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
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

            return raw.Substring(0, lastClosingBrace + 1) + "\n  ]\n}";
        }
    }
}
