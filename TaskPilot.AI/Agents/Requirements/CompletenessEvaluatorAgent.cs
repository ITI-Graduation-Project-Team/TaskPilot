using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class CompletenessEvaluatorAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public CompletenessEvaluatorAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<CompletenessReport>
            EvaluateAsync(
                RequirementSession session)
        {
            var kernel =
                _kernelService
                    .CreateKernel(
                        ModelConstants
                            .CheapModel);

            // Load YAML prompt
            var prompt =
                await _promptLoader
                    .LoadAsync(
                        "Requirements/Completeness.yaml");

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
                session
                    .Requirements
                    .ToPromptText();

            arguments["ambiguities"] =
                string.Join(
                    "\n",
                    session
                        .DetectedAmbiguities);

            arguments["responses"] =
                session
                    .GetUserMessagesAsText();

            // Invoke function
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            var json =
                result.ToString()
                      .Trim();

            var report =
                JsonSerializer.Deserialize
                    <CompletenessReport>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive =
                                true
                        });

            if (report is null)
            {
                throw new Exception(
                    "Failed to parse completeness report.");
            }

            report.Score =
                Math.Clamp(
                    report.Score,
                    0f,
                    1f);

            return report;
        }
    }
}
