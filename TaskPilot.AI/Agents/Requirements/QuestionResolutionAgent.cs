using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Questions;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class QuestionResolutionAgent
    {
        private readonly IAiKernelService
            _kernelService;

        private readonly IPromptLoaderService
            _promptLoader;

        public QuestionResolutionAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService =
                kernelService;

            _promptLoader =
                promptLoader;
        }

        public async Task<
            List<QuestionResolution>>
                ResolveAsync(
                    List<ClarificationQuestion>
                        questions,

                    string pmResponse)
        {
            var kernel =
                _kernelService
                    .CreateKernel(
                        ModelConstants
                            .CheapModel);

            // Load YAML prompt
            var prompt =
                await _promptLoader
                    .LoadAsync(
                        "Requirements/QuestionResolution.yaml");

            // Create YAML function
            var function =
                KernelFunctionYaml
                    .FromPromptYaml(
                        prompt);

            // Create deterministic arguments
            var arguments =
                KernelArgumentsFactory
                    .CreateDeterministicArguments();

            // Send only necessary data
            arguments["questions"] =
                JsonSerializer.Serialize(
                    questions.Select(q => new
                    {
                        questionId =
                            q.Id,

                        question =
                            q.Question
                    }));

            arguments["response"] =
                pmResponse;

            // Invoke
            var result =
                await kernel.InvokeAsync(
                    function,
                    arguments);

            var json =
                result.ToString()
                      .Trim();

            List<QuestionResolution>
                resolutions;

            try
            {
                resolutions =
                    JsonSerializer.Deserialize
                        <List<QuestionResolution>>(
                            json,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive =
                                    true
                            })

                    ??

                    new();
            }
            catch
            {
                return new();
            }

            // Normalize results
            foreach (var item
                     in resolutions)
            {
                item.ExtractedAnswer ??=
                    string.Empty;
            }

            return resolutions;
        }
    }
}
