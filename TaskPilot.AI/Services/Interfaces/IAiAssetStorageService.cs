using System.IO;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Common;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IAiAssetStorageService
    {
        Task<Result<FileUploadResultDto>> UploadAssetAsync(Stream fileStream, string fileName, string folder);
    }
}
