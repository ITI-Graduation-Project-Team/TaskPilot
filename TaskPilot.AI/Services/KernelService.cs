using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services
{
    public class KernelService
        : IAiKernelService
    {
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;

        public KernelService(
            IConfiguration config,
            IServiceProvider serviceProvider)
        {
            _config = config;
            _serviceProvider = serviceProvider;
        }

        public Kernel CreateKernel(
            string modelId,
            string? httpClientName = null)
        {
            var apiKey =
                _config["OpenAI:ApiKey"];

            var builder =
                Kernel.CreateBuilder();

            if (!string.IsNullOrEmpty(httpClientName))
            {
                var httpClientFactory = _serviceProvider.GetService<System.Net.Http.IHttpClientFactory>();
                if (httpClientFactory != null)
                {
                    var httpClient = httpClientFactory.CreateClient(httpClientName);
                    builder.AddOpenAIChatCompletion(
                        modelId: modelId,
                        apiKey: apiKey!,
                        httpClient: httpClient);
                }
                else
                {
                    builder.AddOpenAIChatCompletion(
                        modelId: modelId,
                        apiKey: apiKey!);
                }
            }
            else
            {
                builder.AddOpenAIChatCompletion(
                    modelId: modelId,
                    apiKey: apiKey!);
            }

            builder.Services.AddSingleton(new AiKernelModelDescriptor(modelId));

            var usageRecorder = _serviceProvider.GetService<IAiUsageRecorder>();
            if (usageRecorder != null)
            {
                builder.Services.AddSingleton(usageRecorder);
            }

            var filter = _serviceProvider.GetService<IFunctionInvocationFilter>();
            if (filter != null)
            {
                builder.Services.AddSingleton(filter);
            }

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

            builder.Services.AddSingleton(new AiKernelModelDescriptor(modelId));

            var usageRecorder = _serviceProvider.GetService<IAiUsageRecorder>();
            if (usageRecorder != null)
            {
                builder.Services.AddSingleton(usageRecorder);
            }

            var filter = _serviceProvider.GetService<IFunctionInvocationFilter>();
            if (filter != null)
            {
                builder.Services.AddSingleton(filter);
            }

            return builder.Build();
        }
    }
}
