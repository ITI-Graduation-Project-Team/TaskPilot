using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services
{
    public class KernelService
        : IAiKernelService
    {
        private readonly IConfiguration
            _config;

        public KernelService(
            IConfiguration config)
        {
            _config = config;
        }

        public Kernel CreateKernel(
            string modelId)
        {
            var apiKey =
                _config["OpenAI:ApiKey"];

            var builder =
                Kernel.CreateBuilder();

            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey!);

            return builder.Build();
        }
        public Kernel CreateGeminiKernel(
            string modelId)
        {
            var apiKey =
                _config["Gemini:ApiKey"];

            var builder =
                Kernel.CreateBuilder();

            builder.AddGoogleAIGeminiChatCompletion(
                modelId: modelId,
                apiKey: apiKey!);

            return builder.Build();
        }
    }
}