using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.Sprint;

namespace TaskPilot.AI.Agents.Planning
{
    public class SprintRetrospectiveAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public SprintRetrospectiveAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<(SprintAnalysisDto Analysis, List<SprintImprovementDto> Improvements)> AnalyzeAsync(
            SprintRetrospectiveData data,
            string userLanguage = "English",
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync("Planning/SprintRetrospective.yaml");

            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var metricsJson = JsonSerializer.Serialize(new
            {
                completionRate      = Math.Round(data.CompletionRate, 1),
                velocityRatio       = Math.Round(data.VelocityRatio, 2),
                totalTasks          = data.TotalTasks,
                completedTasks      = data.CompletedTasks,
                unfinishedTasks     = data.UnfinishedTasks.Count,
                totalEstimatedHours = data.TotalEstimatedHours,
                totalActualHours    = data.TotalActualHours,
                developers          = data.DeveloperBreakdowns.Select(d => new
                {
                    name              = d.FullName,
                    completionRate    = Math.Round(d.CompletionRate, 1),
                    velocityRatio     = Math.Round(d.VelocityRatio, 2),
                    estimatedHours    = d.EstimatedHours,
                    actualHours       = d.ActualHours,
                    assignedTasks     = d.AssignedTasks,
                    completedTasks    = d.CompletedTasks
                }),
                unfinishedTaskDetails = data.UnfinishedTasks.Select(t => new
                {
                    title          = t.TitleEn,
                    estimatedHours = t.EstimatedHours,
                    reason         = t.Reason,
                    assignee       = t.AssigneeName
                })
            });

            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["sprintTitle"]   = data.SprintTitleEn,
                    ["metricsJson"]   = metricsJson,
                    ["userLanguage"]  = userLanguage
                },
                cancellationToken: cancellationToken);

            var raw = result.ToString().Trim();

            if (raw.StartsWith("```json"))
            {
                raw = raw.Substring(7);
            }
            if (raw.StartsWith("```"))
            {
                raw = raw.Substring(3);
            }
            if (raw.EndsWith("```"))
            {
                raw = raw.Substring(0, raw.Length - 3);
            }
            raw = raw.Trim();

            try
            {
                var parsed = JsonSerializer.Deserialize<RetrospectiveAgentOutput>(
                    raw,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return (
                    parsed?.Analysis ?? new SprintAnalysisDto(),
                    parsed?.Improvements ?? new List<SprintImprovementDto>()
                );
            }
            catch (JsonException)
            {
                return (new SprintAnalysisDto(), new List<SprintImprovementDto>());
            }
        }
    }

    internal class RetrospectiveAgentOutput
    {
        public SprintAnalysisDto Analysis { get; set; } = new();
        public List<SprintImprovementDto> Improvements { get; set; } = new();
    }
}
