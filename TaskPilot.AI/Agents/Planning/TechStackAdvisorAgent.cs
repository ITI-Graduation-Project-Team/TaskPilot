using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Models.Entities;

namespace TaskPilot.AI.Agents.Planning
{
    public class TechStackAdvisorAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public TechStackAdvisorAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<TechStackSuggestion> SuggestAsync(
            RequirementsSnapshot snapshot,
            List<EmployeeSkillSummary> availableSkills,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(
                ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync(
                "Planning/TechStackAdvisor.yaml");

            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            // Format skills for the prompt
            var skillsText = availableSkills != null && availableSkills.Any()
                ? string.Join("\n", availableSkills
                    .Select(s =>
                        $"- {s.SkillName}: {s.EmployeeCount} developer(s), " +
                        $"{s.AvailableFte:0.##} FTE, max level: {s.MaxLevel}, " +
                        $"levels [Beginner: {s.BeginnerCount}, Intermediate: {s.IntermediateCount}, " +
                        $"Advanced: {s.AdvancedCount}, Expert: {s.ExpertCount}]"))
                : "No employee skill data available.";

            var result = await kernel.InvokeAsync(
                function,
                new KernelArguments
                {
                    ["businessRequirements"] =
                        string.Join("\n", snapshot.BusinessRequirements),
                    ["technicalRequirements"] =
                        string.Join("\n", snapshot.TechnicalRequirements),
                    ["constraints"] =
                        string.Join("\n", snapshot.Constraints),
                    ["integrations"] =
                        string.Join("\n", snapshot.Integrations),
                    ["scaleRequirements"] =
                        string.Join("\n", snapshot.ScaleRequirements),
                    ["availableSkills"] = skillsText
                },
                cancellationToken: cancellationToken);

            var raw = result.ToString();

            try
            {
                var suggestion = JsonSerializer.Deserialize<TechStackSuggestion>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (suggestion is null)
                    throw new TechStackAdvisorException(
                        "Tech stack suggestion returned null.", raw);

                ValidateSuggestion(suggestion, raw);
                return suggestion;
            }
            catch (JsonException ex)
            {
                throw new TechStackAdvisorException(
                    $"Tech stack suggestion returned invalid JSON: {ex.Message}",
                    raw);
            }
        }

        private static void ValidateSuggestion(TechStackSuggestion suggestion, string raw)
        {
            var allowedPlatforms = new HashSet<string>(new[] { "Web", "Mobile", "Desktop", "API" }, System.StringComparer.OrdinalIgnoreCase);
            var allowedProjectTypes = new HashSet<string>(new[] { "ERP", "SaaS", "MobileApp", "API", "Portal", "Other" }, System.StringComparer.OrdinalIgnoreCase);
            var allowedGapTypes = new HashSet<string>(new[] { "MissingSkill", "ProficiencyGap", "CapacityGap", "Unclassified" }, System.StringComparer.OrdinalIgnoreCase);
            var allowedSeverities = new HashSet<string>(new[] { "Low", "Medium", "High" }, System.StringComparer.OrdinalIgnoreCase);

            var valid = suggestion.PrimaryStack.TechStack.Count > 0
                && suggestion.IdealStack.TechStack.Count > 0
                && suggestion.PlatformTargets.Count > 0
                && suggestion.PlatformTargets.All(allowedPlatforms.Contains)
                && allowedProjectTypes.Contains(suggestion.ProjectType)
                && suggestion.GapAnalysis.All(gap =>
                    !string.IsNullOrWhiteSpace(gap.Summary)
                    && allowedGapTypes.Contains(gap.GapType)
                    && allowedSeverities.Contains(gap.Severity));

            if (!valid)
                throw new TechStackAdvisorException("Tech stack suggestion did not match the required contract.", raw);
        }
    }
}
