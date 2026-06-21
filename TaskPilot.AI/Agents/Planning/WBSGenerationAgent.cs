using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;
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
            var promptTemplate = await _promptLoader.LoadAsync("Planning/WbsGeneration.yaml");
            
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel); // Use powerful model

            var snapshotJson = JsonSerializer.Serialize(new
            {
                snapshot.BusinessRequirements,
                snapshot.TechnicalRequirements,
                snapshot.Constraints,
                snapshot.Integrations,
                snapshot.ScaleRequirements
            }, new JsonSerializerOptions { WriteIndented = true });

            var arguments = new KernelArguments
            {
                ["snapshotData"] = snapshotJson
            };

            var promptRendered = await kernel.InvokePromptAsync(promptTemplate, arguments, cancellationToken: cancellationToken);
            var responseText = promptRendered.GetValue<string>()?.Trim();

            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new WbsGenerationException("Model returned an empty response.");
            }

            // Clean up common markdown block formatting if present
            if (responseText.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                responseText = responseText.Substring(7);
            }
            else if (responseText.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                responseText = responseText.Substring(3);
            }

            if (responseText.EndsWith("```"))
            {
                responseText = responseText.Substring(0, responseText.Length - 3);
            }

            responseText = responseText.Trim();

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var generatedWbs = JsonSerializer.Deserialize<GeneratedWbs>(responseText, options);
                
                if (generatedWbs == null || generatedWbs.Sprints == null)
                {
                    throw new WbsGenerationException("Deserialized output was null or missing expected structure.");
                }

                return generatedWbs;
            }
            catch (JsonException ex)
            {
                var snippetLength = Math.Min(responseText.Length, 500);
                var snippet = responseText.Substring(0, snippetLength);
                throw new WbsGenerationException($"Failed to deserialize WBS from model output. Start of response: {snippet}...", ex);
            }
        }
    }
}
