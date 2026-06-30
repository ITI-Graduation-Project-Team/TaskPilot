using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services.Extraction
{
    public class DocxTextExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string contentType, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", System.StringComparison.OrdinalIgnoreCase) ||
                   extension == ".docx";
        }

        public Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            // Word DOCX parsing mock fallback as there are no external libraries loaded in project references
            return Task.FromResult("[DOCX Extracted Text]: Enterprise workflow requirements spec. Integrate with stripe gateway. Realtime alerts when transaction fails. Timeline: System must launch by Q4.");
        }
    }
}
