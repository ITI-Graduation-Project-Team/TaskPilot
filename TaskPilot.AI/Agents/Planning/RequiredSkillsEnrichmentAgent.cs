using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.AI.Constants;

namespace TaskPilot.AI.Agents.Planning
{
    public class RequiredSkillsEnrichmentAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly ILogger<RequiredSkillsEnrichmentAgent> _logger;

        public RequiredSkillsEnrichmentAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            ILogger<RequiredSkillsEnrichmentAgent> logger)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _logger = logger;
        }

        public async Task<Result<List<GeneratedRequiredSkill>>> EnrichAsync(
            string titleEn,
            string descriptionEn,
            string taskType,
            List<string> availableSkills,
            CancellationToken cancellationToken = default)
        {
            if (taskType?.Equals("NonTechnical", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Result.Success(new List<GeneratedRequiredSkill>());
            }

            var promptYaml = await _promptLoader.LoadAsync("Planning/RequiredSkillsEnrichment.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(promptYaml);
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);

            var arguments = new KernelArguments
            {
                ["title"] = titleEn,
                ["description"] = descriptionEn,
                ["availableSkills"] = JsonSerializer.Serialize(availableSkills)
            };

            try
            {
                var response = await kernel.InvokeAsync(function, arguments, cancellationToken);
                var resultText = response.GetValue<string>();

                if (string.IsNullOrWhiteSpace(resultText))
                    return Result.Failure<List<GeneratedRequiredSkill>>(WbsErrors.InvalidGeneratedSkillJson);

                // Simple JSON extraction assuming it returns an array
                int startIndex = resultText.IndexOf('[');
                int endIndex = resultText.LastIndexOf(']');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    var json = resultText.Substring(startIndex, endIndex - startIndex + 1);
                    var skills = JsonSerializer.Deserialize<List<GeneratedRequiredSkill>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (skills != null)
                        return Result.Success(skills);
                }
                
                return Result.Failure<List<GeneratedRequiredSkill>>(WbsErrors.InvalidGeneratedSkillJson);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "AI skill enrichment invocation was canceled or timed out.");
                return Result.Failure<List<GeneratedRequiredSkill>>(new Error("EnrichmentCanceled", ErrorType.Failure, "AI invocation was canceled."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enrich required skills or parse JSON from AI.");
                return Result.Failure<List<GeneratedRequiredSkill>>(new Error("EnrichmentFailed", ErrorType.Failure, "Failed to invoke or process AI enrichment."));
            }
        }
    }
}
