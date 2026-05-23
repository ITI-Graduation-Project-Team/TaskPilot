using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TaskPilot.DTOs.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Infrastructure.Services.Storage
{
    public class CloudinaryService
        : IFileStorageService
    {
        private readonly Cloudinary
            _cloudinary;

        public CloudinaryService(
            IConfiguration config)
        {
            var account =
                new Account(
                    config["Cloudinary:CloudName"],
                    config["Cloudinary:ApiKey"],
                    config["Cloudinary:ApiSecret"]);

            _cloudinary =
                new Cloudinary(account);
        }

        public async Task<
            Result<FileUploadResultDto>>
            UploadFileAsync(
                IFormFile file,
                string folder)
        {
            if (file == null ||
                file.Length == 0)
            {
                return Result.Failure<
                    FileUploadResultDto>(
                    CommonErrors.InvalidInput(
                        "Invalid file."));
            }

            await using var stream =
                file.OpenReadStream();

            var uploadParams =
                new RawUploadParams
                {
                    File =
                        new FileDescription(
                            file.FileName,
                            stream),

                    Folder = folder
                };

            var result =
                await _cloudinary
                    .UploadAsync(
                        uploadParams);

            if (result.Error != null)
            {
                return Result.Failure<
                    FileUploadResultDto>(
                    CommonErrors.ServerError(
                        result.Error.Message));
            }

            return new FileUploadResultDto
            {
                Url =
                    result.SecureUrl
                        .ToString(),

                PublicId =
                    result.PublicId
            };
        }

        public async Task<Result>
            DeleteFileAsync(
                string publicId)
        {
            var deleteParams =
                new DeletionParams(
                    publicId)
                {
                    ResourceType =
                        ResourceType.Raw
                };

            var result =
                await _cloudinary
                    .DestroyAsync(
                        deleteParams);

            if (result.Error != null)
            {
                return Result.Failure(
                    CommonErrors.ServerError(
                        result.Error.Message));
            }

            return Result.Success();
        }

        public async Task<Stream>
        DownloadFileAsync(
        string fileUrl)
        {
            using var httpClient =
                new HttpClient();

            var response =
                await httpClient
                    .GetAsync(fileUrl);

            response.EnsureSuccessStatusCode();

            var memoryStream =
                new MemoryStream();

            await response.Content
                .CopyToAsync(memoryStream);

            memoryStream.Position = 0;

            return memoryStream;
        }
    }

}