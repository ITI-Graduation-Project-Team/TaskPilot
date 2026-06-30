using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Questions;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Ingestion
{
    public class DocumentQuestionResolutionAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public DocumentQuestionResolutionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<List<QuestionResolution>> ResolveAsync(
            List<ClarificationQuestion> questions,
            string documentText)
        {
            if (questions == null || !questions.Any() || string.IsNullOrWhiteSpace(documentText))
            {
                return new List<QuestionResolution>();
            }

            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
            var prompt = await _promptLoader.LoadAsync("Ingestion/DocumentQuestionResolution.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(prompt);

            var arguments = KernelArgumentsFactory.CreateDeterministicArguments();

            arguments["questions"] = JsonSerializer.Serialize(
                questions.Select(q => new
                {
                    questionId = q.Id,
                    question = q.Question
                }));

            arguments["documentText"] = documentText;

            var result = await kernel.InvokeAsync(function, arguments);
            var json = result.ToString().Trim();

            List<QuestionResolution> resolutions;

            try
            {
                resolutions = JsonSerializer.Deserialize<List<QuestionResolution>>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<QuestionResolution>();
            }
            catch
            {
                return new List<QuestionResolution>();
            }

            foreach (var item in resolutions)
            {
                item.ExtractedAnswer ??= string.Empty;
            }

            return resolutions;
        }
    }
}
