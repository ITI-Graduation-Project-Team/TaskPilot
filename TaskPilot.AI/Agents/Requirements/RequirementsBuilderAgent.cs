using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Workflow;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class RequirementsBuilderAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public RequirementsBuilderAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<StructuredRequirements>
            BuildAsync(
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
                        "Requirements/Builder.yaml");

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

            arguments["responses"] =
                session
                    .GetUserMessagesAsText();

            arguments["ambiguities"] =
                string.Join(
                    "\n",
                    session
                        .DetectedAmbiguities);

            arguments["conversationHistory"] =
                string.Join(
                    "\n",
                    session
                        .ConversationHistory
                        .Select(x =>
                            $"{x.Role}: {x.Message}"));

            // Invoke AI
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            var json =
                result.ToString()
                      .Trim();

            var structuredRequirements =
                JsonSerializer.Deserialize
                    <StructuredRequirements>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive =
                                true
                        });

            if (structuredRequirements
                is null)
            {
                throw new Exception(
                    "Failed to build structured requirements.");
            }

            // Save decision using extension
            session.AddDecision(
                nameof(RequirementsBuilderAgent),
                "Structured requirements document generated");

            return structuredRequirements;
        }
    }
}
