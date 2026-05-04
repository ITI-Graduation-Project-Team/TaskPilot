using Microsoft.AspNetCore.Http;

namespace TaskPilot.Services.Interfaces.CVExtractorInterfaces
{
    public interface IFileTextExtractor
    {
        Task<string> ExtractTextAsync(IFormFile file);
    }
}
