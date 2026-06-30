using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default);

        Task<List<float[]>> GenerateEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken = default);
    }
}
