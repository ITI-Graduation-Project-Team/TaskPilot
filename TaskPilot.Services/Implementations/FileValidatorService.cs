using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class FileValidatorService : IFileValidatorService
    {
        private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        private static readonly byte[] JpegMagicBytes1 = { 0xFF, 0xD8, 0xFF, 0xDB };
        private static readonly byte[] JpegMagicBytes2 = { 0xFF, 0xD8, 0xFF, 0xE0 };
        private static readonly byte[] JpegMagicBytes3 = { 0xFF, 0xD8, 0xFF, 0xE1 };
        private static readonly byte[] PngMagicBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public async Task<Result> ValidateAsync(IFormFile file, FileType[] allowedTypes, long maxFileSizeBytes, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                return Result.Failure(CommonErrors.InvalidInput("File is empty or null."));
            }

            if (file.Length > maxFileSizeBytes)
            {
                return Result.Failure(CommonErrors.InvalidInput($"File exceeds the maximum allowed size of {maxFileSizeBytes / (1024 * 1024)}MB."));
            }

            using var stream = file.OpenReadStream();
            
            // Try to match against allowed types
            foreach (var type in allowedTypes)
            {
                stream.Position = 0;
                bool isValid = await ValidateTypeAsync(stream, type, cancellationToken);
                if (isValid)
                {
                    return Result.Success();
                }
            }

            return Result.Failure(CommonErrors.InvalidInput("File type is not allowed or content is invalid."));
        }

        private async Task<bool> ValidateTypeAsync(Stream stream, FileType type, CancellationToken cancellationToken)
        {
            try
            {
                switch (type)
                {
                    case FileType.Pdf:
                        return await ValidateMagicBytesAsync(stream, PdfMagicBytes, cancellationToken);
                    case FileType.Jpeg:
                        return await ValidateMagicBytesAsync(stream, JpegMagicBytes1, cancellationToken) ||
                               await ValidateMagicBytesAsync(stream, JpegMagicBytes2, cancellationToken) ||
                               await ValidateMagicBytesAsync(stream, JpegMagicBytes3, cancellationToken);
                    case FileType.Png:
                        return await ValidateMagicBytesAsync(stream, PngMagicBytes, cancellationToken);
                    case FileType.Docx:
                        try
                        {
                            using var doc = WordprocessingDocument.Open(stream, false);
                            return doc.MainDocumentPart != null;
                        }
                        catch
                        {
                            return false;
                        }
                    case FileType.Txt:
                        return await ValidateTextFileAsync(stream, cancellationToken);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateMagicBytesAsync(Stream stream, byte[] expectedBytes, CancellationToken cancellationToken)
        {
            if (stream.Length < expectedBytes.Length) return false;
            
            stream.Position = 0;
            var buffer = new byte[expectedBytes.Length];
            await stream.ReadExactlyAsync(buffer, 0, expectedBytes.Length, cancellationToken);
            
            return buffer.SequenceEqual(expectedBytes);
        }

        private async Task<bool> ValidateTextFileAsync(Stream stream, CancellationToken cancellationToken)
        {
            stream.Position = 0;
            // Read up to 8KB to check for null bytes
            var buffer = new byte[Math.Min(8192, stream.Length)];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0x00) // Null byte indicates binary
                {
                    return false;
                }
            }
            return true;
        }
    }
}
