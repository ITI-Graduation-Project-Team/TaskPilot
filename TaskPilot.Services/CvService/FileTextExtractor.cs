using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;
using UglyToad.PdfPig;

namespace TaskPilot.Services
{
    public class FileTextExtractor : IFileTextExtractor
    {
        public async Task<string> ExtractTextAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            var extension = Path
                .GetExtension(file.FileName)
                .ToLowerInvariant();

            using var stream = file.OpenReadStream();

            return extension switch
            {
                ".pdf" => ExtractFromPdf(stream),

                ".docx" => ExtractFromWord(stream),

                _ => string.Empty
            };
        }

        private string ExtractFromPdf(Stream stream)
        {
            using var document = PdfDocument.Open(stream);

            var builder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
            }

            return builder.ToString();
        }

        private string ExtractFromWord(Stream stream)
        {
            using var document =
                WordprocessingDocument.Open(stream, false);

            return document.MainDocumentPart?
                       .Document?
                       .Body?
                       .InnerText
                   ?? string.Empty;
        }
    }
}