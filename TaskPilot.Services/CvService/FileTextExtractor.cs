using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;
using UglyToad.PdfPig;

public class FileTextExtractor : IFileTextExtractor
{
    public async Task<string> ExtractTextAsync(IFormFile file)
    {
        if (file.Length == 0)
            throw new Exception("Empty file");

        var extension = Path.GetExtension(file.FileName).ToLower();

        using var stream = file.OpenReadStream();

        return extension switch
        {
            ".pdf" => ExtractFromPdf(stream),
            ".docx" => ExtractFromWord(stream),
            _ => throw new Exception("Unsupported file type")
        };
    }

    private string ExtractFromPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);

        var text = "";

        foreach (var page in document.GetPages())
        {
            text += page.Text + " ";
        }

        return text;
    }

    private string ExtractFromWord(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        return doc.MainDocumentPart?.Document?.Body?.InnerText ?? "";
    }
}