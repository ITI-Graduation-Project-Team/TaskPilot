using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Planning
{
    public class SprintRetrospectiveAgent(
        IAiKernelService kernelService,
        IPromptLoaderService promptLoader)
    {
        public async Task<RetrospectiveResultDto> AnalyzeSprintAsync(
            string sprintGoal,
            string completedTasksJson,
            string delayedTasksJson,
            string commentsJson,
            CancellationToken cancellationToken = default)
        {
            var kernel = kernelService.CreateKernel(ModelConstants.PowerfulModel);
            
            var prompt = await promptLoader.LoadAsync("Planning/SprintRetrospective.yaml");
            
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["sprintGoal"] = sprintGoal,
                    ["completedTasksJson"] = completedTasksJson,
                    ["delayedTasksJson"] = delayedTasksJson,
                    ["commentsJson"] = commentsJson
                },
                cancellationToken: cancellationToken);

            var raw = result.ToString();

            return JsonSerializer.Deserialize<RetrospectiveResultDto>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new RetrospectiveResultDto();
        }
    }
}
