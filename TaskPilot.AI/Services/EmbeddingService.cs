using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
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
            builder.AddOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey!);
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
            var results = await _embeddingGenerator.GenerateEmbeddingsAsync(texts, cancellationToken: cancellationToken);
            var list = new List<float[]>();
            foreach (var r in results)
            {
                list.Add(r.ToArray());
            }
            return list;
        }
    }
}
#pragma warning restore SKEXP0001
