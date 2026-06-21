using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;
using DocumentFormat.OpenXml.Packaging;

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

        public async Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            using var document = WordprocessingDocument.Open(fileStream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var para in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                var text = para.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }
            return await Task.FromResult(sb.ToString().TrimEnd());
        }
    }
}
