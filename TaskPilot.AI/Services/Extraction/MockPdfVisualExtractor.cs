using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services.Extraction
{
    public class MockPdfVisualExtractor : IDocumentVisualExtractor
    {
        public bool CanHandle(string contentType, string fileName)
        {
            return contentType.Contains("pdf") || fileName.EndsWith(".pdf", System.StringComparison.OrdinalIgnoreCase);
        }

        public Task<List<ExtractedVisualFile>> ExtractImagesAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            // For now, in this sandbox environment without PDF parsing dependencies,
            // we return an empty list or a simulated diagram. We will return empty to not crash the pipeline.
            // In a real environment, this would use UglyToad.PdfPig to parse raw bytes.
            return Task.FromResult(new List<ExtractedVisualFile>());
        }
    }
}
