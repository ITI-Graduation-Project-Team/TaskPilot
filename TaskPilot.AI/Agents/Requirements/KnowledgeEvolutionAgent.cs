using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class KnowledgeEvolutionAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public KnowledgeEvolutionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<KnowledgeEvolutionResult> EvaluateAsync(
            RequirementSession session,
            string message,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("Requirements/KnowledgeEvolution.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var arguments = KernelArgumentsFactory.CreateDeterministicArguments();
            
            // Format KB for prompt
            var kbText = string.Join("\n", session.ConsolidatedKnowledgeBase.Select(r => $"[{r.Id}] ({r.Category}) {r.NormalizedText}"));
            if (string.IsNullOrWhiteSpace(kbText)) kbText = "Empty";

            arguments["knowledgeBase"] = kbText;
            arguments["message"] = message;

            var result = await kernel.InvokeAsync(function, arguments, cancellationToken);
            var json = result.ToString().Trim();

            try
            {
                var evolutionResult = JsonSerializer.Deserialize<KnowledgeEvolutionResult>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return evolutionResult ?? new KnowledgeEvolutionResult();
            }
            catch
            {
                return new KnowledgeEvolutionResult();
            }
        }
    }
}
