using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
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

        public async Task<SprintSuggestionDto> SuggestSprintAsync(
            Guid projectId,
            string projectName,
            int sprintDurationInDays,
            decimal targetSprintHours,
            string backlogJson,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(
                ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync(
                "Planning/SprintSuggestion.yaml");

            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["projectId"] = projectId.ToString(),
                    ["projectName"] = projectName,
                    ["sprintDurationInDays"] = sprintDurationInDays.ToString(),
                    ["targetSprintHours"] = targetSprintHours.ToString(),
                    ["backlog"] = backlogJson
                },
                cancellationToken: cancellationToken);

            var raw = result.ToString();

            // Handle potential markdown code block format from AI just in case
            if (raw.StartsWith("```json"))
            {
                raw = raw.Substring(7);
            }
            if (raw.EndsWith("```"))
            {
                raw = raw.Substring(0, raw.Length - 3);
            }

            try
            {
                var suggestion = JsonSerializer.Deserialize<SprintSuggestionDto>(
                    raw,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (suggestion is null || !suggestion.Stories.Any())
                    throw new InvalidOperationException("Sprint suggestion returned empty or null result.");

                return suggestion;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Sprint suggestion returned invalid JSON: {ex.Message}. Raw AI output: {raw}", ex);
            }
        }
    }
}
