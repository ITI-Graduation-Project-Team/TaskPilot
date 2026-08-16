using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.DTOs.Projects;

namespace TaskPilot.Services.Interfaces
{
    /// <summary>
    /// Business logic contract for Project-related operations.
    /// </summary>
    public interface IProjectService
    {
        Task<Result<ProjectDto>> GetByIdAsync(Guid id);
        Task<Result<List<ProjectDto>>> GetAllAsync();
        Task<Result<IEnumerable<ProjectDto>>> GetByCompanyIdAsync(Guid companyId);
        Task<Result<ProjectDto>> CreateAsync(CreateProjectDto projectDto);
        Task<Result> UpdateAsync(UpdateProjectDto projectDto);
        Task<Result> DeleteAsync(Guid id);
        
        Task<Result<ProjectStatusDto>> GetStatusAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<ProjectStatusDto>> UpdateStatusAsync(Guid projectId, ProjectStatusUpdateRequest request, string userId, CancellationToken cancellationToken = default);
        Task<Result<List<ProjectStatusTransitionDto>>> GetAvailableTransitionsAsync(Guid projectId, CancellationToken cancellationToken = default);
        Task<Result<IEnumerable<ProjectDto>>> GetProjectsByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
        
        Task<Result<PagedResult<ProjectDto>>> GetProjectsByCompanyIdPagedAsync(Guid companyId, int page, int pageSize, string? statusFilter = null, string? searchQuery = null, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<ProjectDto>>> GetProjectsByEmployeeIdPagedAsync(Guid employeeId, int page, int pageSize, string? statusFilter = null, string? searchQuery = null, CancellationToken cancellationToken = default);
    }
}
