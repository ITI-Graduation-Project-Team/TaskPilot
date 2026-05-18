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
    }
}
