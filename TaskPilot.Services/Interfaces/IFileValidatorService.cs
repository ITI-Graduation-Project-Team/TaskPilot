using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public enum FileType
    {
        Pdf,
        Docx,
        Txt,
        Jpeg,
        Png
    }

    public interface IFileValidatorService
    {
        Task<Result> ValidateAsync(IFormFile file, FileType[] allowedTypes, long maxFileSizeBytes, CancellationToken cancellationToken = default);
    }
}
