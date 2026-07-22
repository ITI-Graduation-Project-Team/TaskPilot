using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;
using UglyToad.PdfPig;

namespace TaskPilot.AI.Services.Extraction
{
    public class PdfTextExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string contentType, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return contentType.Equals("application/pdf", System.StringComparison.OrdinalIgnoreCase) ||
                   extension == ".pdf";
        }

        public Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var doc = PdfDocument.Open(fileStream);
            var text = string.Join(" ", doc.GetPages().Select(p => p.Text));

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new System.InvalidOperationException("Failed to extract text from the PDF document. The document may be empty, image-based, or corrupted.");
            }

            return Task.FromResult(text);
        }
    }
}
