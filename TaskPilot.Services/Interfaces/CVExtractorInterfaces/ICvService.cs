using Microsoft.AspNetCore.Http;
using TaskPilot.Models.Common.Results;
namespace TaskPilot.Services.Interfaces.CVExtractorInterfaces
{
    public interface ICvService
    {
        Task<Result<List<string>>> ProcessCvAsync(Guid userId, IFormFile file);
    }
}