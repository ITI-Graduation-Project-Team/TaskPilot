using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.Embeddings;
using System.Diagnostics;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Models.Telemetry;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly EmbeddingClient _embeddingClient;
        private readonly IAiUsageRecorder _usageRecorder;

        public EmbeddingService(IConfiguration config, IAiUsageRecorder usageRecorder)
        {
            var apiKey = config["OpenAI:ApiKey"];
            _embeddingClient = new EmbeddingClient(ModelConstants.EmbeddingModel, apiKey!);
            _usageRecorder = usageRecorder;
        }

        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await _embeddingClient.GenerateEmbeddingsAsync([text], cancellationToken: cancellationToken);
                var result = response.Value;
                await _usageRecorder.RecordUsageAsync(
                    new AiTokenUsage(result.Usage.InputTokenCount, 0, 0),
                    nameof(GenerateEmbeddingAsync),
                    ModelConstants.EmbeddingModel,
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken: CancellationToken.None);
                return result[0].ToFloats().ToArray();
            }
            catch (Exception ex)
            {
                await _usageRecorder.RecordFromMetadataAsync(
                    null,
                    nameof(GenerateEmbeddingAsync),
                    ModelConstants.EmbeddingModel,
                    stopwatch.ElapsedMilliseconds,
                    "Failed",
                    ex.Message,
                    cancellationToken: CancellationToken.None);
                throw;
            }
        }

        public async Task<List<float[]>> GenerateEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken = default)
        {
            var list = new List<float[]>();
            
            // Batch process texts in chunks of 100 to avoid OpenAI rate/token limits
            int batchSize = 100;
            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batchTexts = texts.Skip(i).Take(batchSize).ToList();
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var response = await _embeddingClient.GenerateEmbeddingsAsync(batchTexts, cancellationToken: cancellationToken);
                    var results = response.Value;
                    await _usageRecorder.RecordUsageAsync(
                        new AiTokenUsage(results.Usage.InputTokenCount, 0, 0),
                        nameof(GenerateEmbeddingsAsync),
                        ModelConstants.EmbeddingModel,
                        stopwatch.ElapsedMilliseconds,
                        cancellationToken: CancellationToken.None);

                    foreach (var result in results)
                        list.Add(result.ToFloats().ToArray());
                }
                catch (Exception ex)
                {
                    await _usageRecorder.RecordFromMetadataAsync(
                        null,
                        nameof(GenerateEmbeddingsAsync),
                        ModelConstants.EmbeddingModel,
                        stopwatch.ElapsedMilliseconds,
                        "Failed",
                        ex.Message,
                        cancellationToken: CancellationToken.None);
                    throw;
                }
                
                // Minimal delay to prevent burst limit exhaustion
                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }
            return list;
        }
    }
}
