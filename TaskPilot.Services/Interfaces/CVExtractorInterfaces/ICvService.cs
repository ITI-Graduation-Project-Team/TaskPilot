using Microsoft.AspNetCore.Http;
using TaskPilot.DTOs.CV;
using TaskPilot.Models.Common.Results;
namespace TaskPilot.Services.Interfaces.CVExtractorInterfaces
{
    public interface ICvService
    {
        Task<Result<ParsedCvDto>> ExtractAsync(
                   Guid userId,
                   IFormFile file);
    }
}