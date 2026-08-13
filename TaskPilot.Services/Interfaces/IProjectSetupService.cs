using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IProjectSetupService
    {
        Task<Result<ProjectSetupDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ProjectSetupDto>> GenerateTechStackSuggestionAsync(Guid projectId, bool regenerate, CancellationToken cancellationToken = default);
        Task<Result<ProjectSetupDto>> ConfirmTechStackAsync(Guid projectId, ConfirmTechStackRequest request, CancellationToken cancellationToken = default);
        Task<Result<ProjectSetupDto>> QueueWbsAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ProjectSetupDto>> QueueSkillsAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
