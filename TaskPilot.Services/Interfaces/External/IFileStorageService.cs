using Microsoft.AspNetCore.Http;
using TaskPilot.DTOs.Common;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces.ExternalServicesInterfaces
{
    public interface IFileStorageService
    {
        Task<Result<FileUploadResultDto>>
            UploadFileAsync(
                IFormFile file,
                string folder);

        Task<Result<FileUploadResultDto>>
            UploadFileStreamAsync(
                Stream fileStream,
                string fileName,
                string folder);

        Task<Result>
            DeleteFileAsync(
                string publicId);
        Task<Stream> DownloadFileAsync(
                string fileUrl);
    }
}
