using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.CV;

namespace TaskPilot.AI.Services
{
    public class CvAiService
        : ICvAiService
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoaderService;

        private static readonly
            JsonSerializerOptions
            SerializerOptions =
                new()
                {
                    PropertyNameCaseInsensitive = true,

                    Converters =
                    {
                        new JsonStringEnumConverter()
                    }
                };

        public CvAiService(
            IAiKernelService
                kernelService,
            IPromptLoaderService
                promptLoaderService)
        {
            _kernelService = kernelService;

            _promptLoaderService = promptLoaderService;
        }

        public async Task<ParsedCvDto>
            ParseCvAsync(
                string text)
        {
            var prompt =
                await _promptLoaderService.LoadAsync(
                    "CV/ParseCvPrompt.yaml");

            var function =
                KernelFunctionYaml.FromPromptYaml(prompt);
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);

            var result =
                await kernel.InvokeAsync(
                    function,
                    new KernelArguments
                    {
                        ["cvText"] = text
                    });

            var content =
                result.ToString();

            content =
                JsonCleaner
                    .Clean(content);

            try
            {
                var parsedCv =
                    JsonSerializer
                        .Deserialize<
                            ParsedCvDto>(
                            content,
                            SerializerOptions);

                return parsedCv
                       ?? new ParsedCvDto();
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Failed to parse CV response.",
                    ex);
            }
        }
    }
}