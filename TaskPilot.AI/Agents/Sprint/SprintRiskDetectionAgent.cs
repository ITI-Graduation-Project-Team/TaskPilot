using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Sprint;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Sprint
{
    public class SprintRiskDetectionAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public SprintRiskDetectionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<DetectedRiskResult> DetectRisksAsync(
            SprintRiskContext context,
            Guid projectId,
            CancellationToken ct = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("Sprint/SprintRiskDetection.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var arguments = new KernelArguments();
            arguments["sprintContext"] = contextJson;
            arguments["projectId"] = projectId;

            var invokeResult = await kernel.InvokeAsync(function, arguments, cancellationToken: ct);
            var rawJson = invokeResult.ToString().Trim();

            if (rawJson.StartsWith("```json"))
            {
                rawJson = rawJson.Substring(7);
                if (rawJson.EndsWith("```"))
                {
                    rawJson = rawJson.Substring(0, rawJson.Length - 3);
                }
                rawJson = rawJson.Trim();
            }

            var result = JsonSerializer.Deserialize<DetectedRiskResult>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new Exception("SprintRiskDetectionAgent failed to parse JSON result.");

            return result;
        }
    }
}
