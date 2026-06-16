using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IDocumentTextExtractor
    {
        bool CanHandle(string contentType, string fileName);

        Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default);
    }
}
