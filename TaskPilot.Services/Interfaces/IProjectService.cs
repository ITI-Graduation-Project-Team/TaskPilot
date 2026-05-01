using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    /// <summary>
    /// Business logic contract for Project-related operations.
    /// </summary>
    public interface IProjectService
    {
        Task<Result<Project>> GetByIdAsync(Guid id);
        Task<Result<IEnumerable<Project>>> GetAllAsync();
        Task<Result<IEnumerable<Project>>> GetByCompanyIdAsync(Guid companyId);
        Task<Result<Project>> CreateAsync(Project project);
        Task<Result> UpdateAsync(Project project);
        Task<Result> DeleteAsync(Guid id);
    }
}
