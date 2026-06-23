using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Models.Entities;

namespace TaskPilot.AI.Agents.Planning
{
    public class WBSGenerationAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public WBSGenerationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<GeneratedWbs> GenerateAsync(
            RequirementsSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(
                ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync(
                "Planning/WbsGeneration.yaml");

            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["businessRequirements"] =
                        string.Join("\n", snapshot.BusinessRequirements),

                    ["technicalRequirements"] =
                        string.Join("\n", snapshot.TechnicalRequirements),

                    ["constraints"] =
                        string.Join("\n", snapshot.Constraints),

                    ["integrations"] =
                        string.Join("\n", snapshot.Integrations),

                    ["scaleRequirements"] =
                        string.Join("\n", snapshot.ScaleRequirements)
                },
                cancellationToken: cancellationToken);

            var raw = result.ToString();

            try
            {
                var wbs = JsonSerializer.Deserialize<GeneratedWbs>(
                    raw,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (wbs is null || !wbs.UserStories.Any())
                    throw new WbsGenerationException(
                        "WBS generation returned empty or null result.",
                        raw);

                return wbs;
            }
            catch (JsonException ex)
            {
                throw new WbsGenerationException(
                    $"WBS generation returned invalid JSON: {ex.Message}",
                    raw);
            }
        }
    }
}
