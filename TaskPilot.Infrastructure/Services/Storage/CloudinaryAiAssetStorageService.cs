using System.IO;
using System.Threading.Tasks;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.Common;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Infrastructure.Services.Storage
{
    public class CloudinaryAiAssetStorageService : IAiAssetStorageService
    {
        private readonly IFileStorageService _fileStorageService;

        public CloudinaryAiAssetStorageService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public async Task<Result<FileUploadResultDto>> UploadAssetAsync(Stream fileStream, string fileName, string folder)
        {
            return await _fileStorageService.UploadFileStreamAsync(fileStream, fileName, folder);
        }
    }
}
