using Microsoft.SemanticKernel;
using System.Text.Json;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class RequirementExtractionAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public RequirementExtractionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<
            RequirementExtractionResult>
                ExtractAsync(
                    string input,
                    ExtractedRequirements?
                        existingRequirements = null)
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
                        "Requirements/Extraction.yaml");

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
                input;

            arguments["existingRequirements"] =
                existingRequirements is null
                    ? string.Empty
                    : existingRequirements
                        .ToPromptText();

            // Invoke AI
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            var json =
                result.ToString()
                      .Trim();

            RequirementExtractionResult?
                    extracted;

            try
            {
                extracted =
                    JsonSerializer.Deserialize
                        <RequirementExtractionResult>(
                            json,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive =
                                    true
                            });
            }
            catch
            {
                throw new Exception(
                    $"Invalid extraction JSON: {json}");
            }

            if (extracted is null)
            {
                throw new Exception(
                    "Failed to parse extraction result.");
            }

            // Normalize output
            NormalizeRequirements(
                extracted);

            // Remove duplicates
            DeduplicateRequirements(
                extracted);

            return extracted;
        }

        private static void
            NormalizeRequirements(
                RequirementExtractionResult
                    requirements)
        {
            requirements.BusinessRequirements =
                NormalizeList(
                    requirements
                        .BusinessRequirements);

            requirements.TechnicalRequirements =
                NormalizeList(
                    requirements
                        .TechnicalRequirements);

            requirements.Constraints =
                NormalizeList(
                    requirements
                        .Constraints);

            requirements.Integrations =
                NormalizeList(
                    requirements
                        .Integrations);

            requirements.ScaleRequirements =
                NormalizeList(
                    requirements
                        .ScaleRequirements);
        }

        private static List<string>
            NormalizeList(
                List<string> items)
        {
            return items

                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x))

                .Select(x =>
                    x.Trim())

                .Where(x =>
                    x.Length > 2)

                .ToList();
        }

        private static void
            DeduplicateRequirements(
                RequirementExtractionResult
                    requirements)
        {
            requirements.BusinessRequirements =
                DistinctList(
                    requirements
                        .BusinessRequirements);

            requirements.TechnicalRequirements =
                DistinctList(
                    requirements
                        .TechnicalRequirements);

            requirements.Constraints =
                DistinctList(
                    requirements
                        .Constraints);

            requirements.Integrations =
                DistinctList(
                    requirements
                        .Integrations);

            requirements.ScaleRequirements =
                DistinctList(
                    requirements
                        .ScaleRequirements);
        }

        private static List<string>
            DistinctList(
                List<string> items)
        {
            return items

                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)

                .ToList();
        }
    }
}
