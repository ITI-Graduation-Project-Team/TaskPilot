using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Services.Interfaces;

#pragma warning disable SKEXP0001
namespace TaskPilot.AI.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly ITextEmbeddingGenerationService _embeddingGenerator;

        public EmbeddingService(IConfiguration config)
        {
            var apiKey = config["OpenAI:ApiKey"];
            // We use the Semantic Kernel extension to create the embedding service
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAITextEmbeddingGeneration(ModelConstants.EmbeddingModel, apiKey!);
            var kernel = builder.Build();
            _embeddingGenerator = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        }

        public async Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            var result = await _embeddingGenerator.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            return result.ToArray();
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
                var results = await _embeddingGenerator.GenerateEmbeddingsAsync(batchTexts, cancellationToken: cancellationToken);
                
                foreach (var r in results)
                {
                    list.Add(r.ToArray());
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
#pragma warning restore SKEXP0001
