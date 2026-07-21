using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Sprint;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Sprint
{
    public class SprintBurnoutAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public SprintBurnoutAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<BurnoutRiskResult> AnalyzeAsync(
            EmployeeSprintBurnoutContext context,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("Sprint/SprintBurnoutAnalysis.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var arguments = KernelArgumentsFactory.CreateDeterministicArguments();
            arguments["employeeSprintContext"] = contextJson;

            var result = await kernel.InvokeAsync(function, arguments, cancellationToken);
            var resultString = result.ToString().Trim();

            try
            {
                var parsedResult = JsonSerializer.Deserialize<BurnoutRiskResult>(resultString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return parsedResult ?? new BurnoutRiskResult();
            }
            catch
            {
                return new BurnoutRiskResult
                {
                    BurnoutScore = 0,
                    RiskLevel = "Healthy",
                    TrendDirection = "stable"
                };
            }
        }
    }
}
