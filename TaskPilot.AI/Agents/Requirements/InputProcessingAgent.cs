using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class InputProcessingAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public InputProcessingAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<string>
            ProcessAsync(
                string rawInput)
        {
            var kernel =
                _kernelService
                    .CreateGeminiKernel(
                        ModelConstants
                            .GeminiFast);

            // Load YAML prompt
            var prompt =
                await _promptLoader
                    .LoadAsync(
                        "Requirements/InputProcessing.yaml");

            // Create YAML function
            var function =
                KernelFunctionYaml
                    .FromPromptYaml(
                        prompt);

            // Create deterministic arguments
            var arguments =
                KernelArgumentsFactory
                    .CreateDeterministicArguments();

            arguments["input"] =
                rawInput;

            // Invoke function
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            return result
                .ToString()
                .Trim();
        }
    }
}
