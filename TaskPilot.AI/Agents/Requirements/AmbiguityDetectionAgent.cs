using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class AmbiguityDetectionAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public AmbiguityDetectionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<List<AmbiguityItem>>
            DetectAsync(
                ExtractedRequirements
                    requirements)
        {
            var kernel =
                _kernelService
                    .CreateKernel(
                        ModelConstants
                            .FastModel);

            // Load YAML prompt
            var prompt =
                await _promptLoader
                    .LoadAsync(
                        "Requirements/Ambiguity.yaml");

            // Create YAML function
            var function =
                KernelFunctionYaml
                    .FromPromptYaml(
                        prompt);

            // Create deterministic arguments
            var arguments =
                KernelArgumentsFactory
                    .CreateDeterministicArguments();

            arguments["requirements"] =
                requirements
                    .ToPromptText();

            // Invoke
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            var json = result.ToString().Trim();

            try
            {
                var ambiguities = JsonSerializer.Deserialize<List<AmbiguityItem>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return ambiguities ?? new List<AmbiguityItem>();
            }
            catch
            {
                return new List<AmbiguityItem>();
            }
        }
    }
}
