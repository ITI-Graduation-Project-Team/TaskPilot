using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Requirements
{
    public class VisualAnalysisAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly ILogger<VisualAnalysisAgent> _logger;
        private readonly ITelemetryAccumulator _telemetry;

        public VisualAnalysisAgent(IAiKernelService kernelService, IPromptLoaderService promptLoader, ILogger<VisualAnalysisAgent> logger, ITelemetryAccumulator telemetry)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task<VisualAnalysisResponse> AnalyzeImageAsync(
            string cloudinaryUrl,
            byte[] imageBytes,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

            var promptYaml = await _promptLoader.LoadAsync("Requirements/VisualAnalysis.yaml");
            
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(promptYaml);
            
            var userMessageContent = new ImageContent(imageBytes, contentType);
            var chatMessage = new ChatMessageContent(AuthorRole.User, "");
            chatMessage.Items.Add(userMessageContent);
            chatHistory.Add(chatMessage);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var reply = await chatCompletion.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            sw.Stop();

            _telemetry.RecordCall(reply?.Metadata, sw.ElapsedMilliseconds, "VisualAnalysisAgent", ModelConstants.PowerfulModel, _logger);
            
            var json = reply?.Content ?? "{}";

            json = CleanJsonString(json);
            
            return JsonSerializer.Deserialize<VisualAnalysisResponse>(json) ?? new VisualAnalysisResponse();
        }

        private string CleanJsonString(string json)
        {
            json = json.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                json = json.Substring(7);
                if (json.EndsWith("```"))
                {
                    json = json.Substring(0, json.Length - 3);
                }
            }
            else if (json.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                json = json.Substring(3);
                if (json.EndsWith("```"))
                {
                    json = json.Substring(0, json.Length - 3);
                }
            }
            return json.Trim();
        }
    }
}
