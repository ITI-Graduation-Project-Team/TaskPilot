using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IAiProjectsService
    {
        Task<Result<BrdUploadResultDto>> UploadBrdAsync(IFormFile file, Guid? projectId, CancellationToken cancellationToken = default);
        Task<Result<AiChatResponseDto>> ProcessChatAsync(SendAiMessageDto request, CancellationToken cancellationToken = default);
        Task<Result<GenerateProjectDto>> GenerateProjectAsync(Guid? projectId, string projectName, CancellationToken cancellationToken = default);
        Task<Result<ProjectChatHistoryDto>> GetChatHistoryAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<AiChatResponseDto>> ProcessFollowUpChatAsync(Guid projectId, string message, CancellationToken cancellationToken = default);
    }
}
