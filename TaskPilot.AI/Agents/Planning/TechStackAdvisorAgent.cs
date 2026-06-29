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
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(
                ModelConstants.PowerfulModel);

            var prompt = await _promptLoader.LoadAsync(
                "Planning/TechStackAdvisor.yaml");

            var function = KernelFunctionYaml.FromPromptYaml(prompt);

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
                        string.Join("\n", snapshot.ScaleRequirements)
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

                return suggestion;
            }
            catch (JsonException ex)
            {
                throw new TechStackAdvisorException(
                    $"Tech stack suggestion returned invalid JSON: {ex.Message}",
                    raw);
            }
        }
    }
}
