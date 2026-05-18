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
        private readonly Kernel
            _kernel;

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
                kernelService)
        {
            _kernel =
                kernelService
                    .CreateKernel(
                        ModelConstants
                            .CheapModel);
        }

        public async Task<ParsedCvDto>
            ParseCvAsync(
                string text)
        {
            var prompt =
                PromptLoader.Load(
                    "Prompts/Cv/ParseCvPrompt.txt");

            var arguments =
                new KernelArguments
                {
                    ["cvText"] = text
                };

            var result =
                await _kernel
                    .InvokePromptAsync(
                        prompt,
                        arguments);

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