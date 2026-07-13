using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.RAG
{
    public class KnowledgeAnswerAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly ILogger<KnowledgeAnswerAgent> _logger;

        public KnowledgeAnswerAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            ILogger<KnowledgeAnswerAgent> logger)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _logger = logger;
        }

        public async Task<string> GenerateAsync(
            string question,
            List<KnowledgeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (chunks == null || chunks.Count == 0)
            {
                return "The uploaded documents do not contain enough information.";
            }

            var context = string.Join("\n\n", chunks.Select((c, i) => $"--- Chunk {i + 1} ---\n{c.Content}"));

            var kernel = _kernelService.CreateKernel(Constants.ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("RAG/KnowledgeAnswer.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var arguments = new KernelArguments
            {
                ["question"] = question,
                ["context"] = context
            };

            var result = await kernel.InvokeAsync(function, arguments, cancellationToken);
            var answer = result?.ToString()?.Trim() ?? "The uploaded documents do not contain enough information.";

            _logger.LogInformation("Generated answer successfully.");
            
            return answer;
        }
    }
}
