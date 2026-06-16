using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services.Extraction
{
    public class TextFileExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string contentType, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return contentType.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase) ||
                   extension == ".txt" ||
                   extension == ".md";
        }

        public async Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(fileStream, leaveOpen: true);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
