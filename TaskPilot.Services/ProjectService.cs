using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services
{
    /// <summary>
    /// Contains all business logic for Project operations.
    /// Accesses data exclusively through IUnitOfWork — never touches DbContext directly.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProjectService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Project>> GetByIdAsync(Guid id)
        {
            var project = await _unitOfWork.Projects.GetByIdAsync(id,
                p => p.Manager,
                p => p.Company,
                p => p.Sprints);

            if (project is null)
                return Result.Failure<Project>(CommonErrors.NotFound("Project"));

            return Result.Success(project);
        }

        public async Task<Result<IEnumerable<Project>>> GetAllAsync()
        {
            var projects = await _unitOfWork.Projects.GetAllAsync(
                p => p.Manager,
                p => p.Company);

            return Result.Success(projects);
        }

        public async Task<Result<IEnumerable<Project>>> GetByCompanyIdAsync(Guid companyId)
        {
            var companyExists = await _unitOfWork.Companies
                .AnyAsync(c => c.Id == companyId);

            if (!companyExists)
                return Result.Failure<IEnumerable<Project>>(CommonErrors.NotFound("Company"));

            var projects = await _unitOfWork.Projects
                .FindAsync(p => p.CompanyId == companyId, p => p.Manager);

            return Result.Success(projects);
        }

        public async Task<Result<Project>> CreateAsync(Project project)
        {
            var managerExists = await _unitOfWork.ProjectManagers
                .AnyAsync(pm => pm.Id == project.ManagerId);

            if (!managerExists)
                return Result.Failure<Project>(CommonErrors.NotFound("Project Manager"));

            var companyExists = await _unitOfWork.Companies
                .AnyAsync(c => c.Id == project.CompanyId);

            if (!companyExists)
                return Result.Failure<Project>(CommonErrors.NotFound("Company"));

            await _unitOfWork.Projects.AddAsync(project);
            return Result.Success(project);
        }

        public async Task<Result> UpdateAsync(Project project)
        {
            var existing = await _unitOfWork.Projects.GetByIdAsync(project.Id);

            if (existing is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            existing.NameEn = project.NameEn;
            existing.NameAr = project.NameAr;
            existing.DescriptionEn = project.DescriptionEn;
            existing.DescriptionAr = project.DescriptionAr;
            existing.ModifiedAt = DateTime.UtcNow;

            _unitOfWork.Projects.Update(existing);
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            var project = await _unitOfWork.Projects.GetByIdAsync(id);

            if (project is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            _unitOfWork.Projects.Delete(project);
            return Result.Success();
        }
    }
}
