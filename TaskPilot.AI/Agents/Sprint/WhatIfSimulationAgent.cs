using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Sprint;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Entities;

namespace TaskPilot.AI.Agents.Sprint
{
    public class WhatIfSimulationAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public WhatIfSimulationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<WhatIfResult> SimulateAsync(
            SprintRiskAlert alert,
            SprintRiskContext context,
            CancellationToken ct = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("Sprint/WhatIfSimulation.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var contextJson = JsonSerializer.Serialize(context, jsonOptions);
            
            var alertSnapshot = new 
            {
                alert.RiskType,
                alert.Severity,
                alert.AffectedTaskId,
                alert.AffectedEmployeeId,
                alert.MessageEn
            };
            var alertJson = JsonSerializer.Serialize(alertSnapshot, jsonOptions);

            var arguments = new KernelArguments();
            arguments["riskAlert"] = alertJson;
            arguments["sprintContext"] = contextJson;

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

            var result = JsonSerializer.Deserialize<WhatIfResult>(rawJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new Exception("WhatIfSimulationAgent failed to parse JSON result.");

            return result;
        }
    }
}
