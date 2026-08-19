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
            // Retrospectives are structured summaries, so the smaller model keeps
            // generation responsive without requiring deep reasoning.
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);

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
                    employeeId        = d.EmployeeId.ToString(),
                    name              = d.FullName,
                    completionRate    = Math.Round(d.CompletionRate, 1),
                    velocityRatio     = Math.Round(d.VelocityRatio, 2),
                    estimatedHours    = d.EstimatedHours,
                    actualHours       = d.ActualHours,
                    assignedTasks     = d.AssignedTasks,
                    completedTasks    = d.CompletedTasks
                }),
                unfinishedTaskDetails = data.UnfinishedTasks.Take(10).Select(t => new
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

                var analysis = parsed?.Analysis ?? new SprintAnalysisDto();
                var improvements = parsed?.Improvements ?? new List<SprintImprovementDto>();

                // Fallback resolution for targetEmployeeId if AI didn't populate it or populated Guid.Empty
                foreach (var imp in improvements)
                {
                    if (!imp.TargetEmployeeId.HasValue || imp.TargetEmployeeId.Value == Guid.Empty)
                    {
                        foreach (var dev in data.DeveloperBreakdowns)
                        {
                            if (dev.EmployeeId == Guid.Empty || string.IsNullOrWhiteSpace(dev.FullName))
                                continue;

                            var nameParts = dev.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var firstName = nameParts.FirstOrDefault();

                            bool isMatch = (!string.IsNullOrEmpty(imp.RecommendationEn) &&
                                            imp.RecommendationEn.Contains(dev.FullName, StringComparison.OrdinalIgnoreCase))
                                        || (!string.IsNullOrEmpty(imp.RecommendationAr) &&
                                            imp.RecommendationAr.Contains(dev.FullName, StringComparison.OrdinalIgnoreCase))
                                        || (!string.IsNullOrEmpty(firstName) && firstName.Length > 2 &&
                                            ((!string.IsNullOrEmpty(imp.RecommendationEn) && imp.RecommendationEn.Contains(firstName, StringComparison.OrdinalIgnoreCase)) ||
                                             (!string.IsNullOrEmpty(imp.RecommendationAr) && imp.RecommendationAr.Contains(firstName, StringComparison.OrdinalIgnoreCase))));

                            if (isMatch)
                            {
                                imp.TargetEmployeeId = dev.EmployeeId;
                                break;
                            }
                        }
                    }
                }

                return (analysis, improvements);
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
