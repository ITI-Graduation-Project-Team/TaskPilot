using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IDocumentVisualExtractor
    {
        bool CanHandle(string contentType, string fileName);
        
        Task<List<ExtractedVisualFile>> ExtractImagesAsync(
            Stream fileStream, 
            CancellationToken cancellationToken = default);
    }

    public class ExtractedVisualFile
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] RawBytes { get; set; } = System.Array.Empty<byte>();
        public int PageNumber { get; set; }
    }
}
