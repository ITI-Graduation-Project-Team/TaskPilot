using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Data.Repositories;

namespace TaskPilot.Services
{
    /// <summary>
    /// Contains all business logic for Project operations.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class ProjectService : IProjectService
    {
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<Company> _companyRepo;
        private readonly IRepository<ProjectManager> _managerRepo;

        public ProjectService(
            IRepository<Project> projectRepo, 
            IRepository<Company> companyRepo,
            IRepository<ProjectManager> managerRepo)
        {
            _projectRepo = projectRepo;
            _companyRepo = companyRepo;
            _managerRepo = managerRepo;
        }

        public async Task<Result<Project>> GetByIdAsync(Guid id)
        {
            var project = await _projectRepo.GetByIdAsync(id, 
                p => p.Manager, 
                p => p.Company, 
                p => p.Sprints);

            if (project is null)
                return Result.Failure<Project>(CommonErrors.NotFound("Project"));

            return Result.Success(project);
        }

        public async Task<Result<List<Project>>> GetAllAsync()
        {
            var projects = await _projectRepo.GetAllAsync(
                p => p.Manager, 
                p => p.Company);

            return Result.Success(projects.ToList());
        }

        public async Task<Result<IEnumerable<Project>>> GetByCompanyIdAsync(Guid companyId)
        {
            var companyExists = await _companyRepo.AnyAsync(c => c.Id == companyId);

            if (!companyExists)
                return Result.Failure<IEnumerable<Project>>(CommonErrors.NotFound("Company"));

            var projects = await _projectRepo.FindAsync(
                p => p.CompanyId == companyId, 
                p => p.Manager);

            return Result.Success(projects.AsEnumerable());
        }

        public async Task<Result<Project>> CreateAsync(Project project)
        {
            var managerExists = await _managerRepo.AnyAsync(pm => pm.Id == project.ManagerId);

            if (!managerExists)
                return Result.Failure<Project>(CommonErrors.NotFound("Project Manager"));

            var companyExists = await _companyRepo.AnyAsync(c => c.Id == project.CompanyId);

            if (!companyExists)
                return Result.Failure<Project>(CommonErrors.NotFound("Company"));

            await _projectRepo.AddAsync(project);
            return Result.Success(project);
        }

        public async Task<Result> UpdateAsync(Project project)
        {
            var existing = await _projectRepo.GetByIdAsync(project.Id);

            if (existing is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            existing.NameEn = project.NameEn;
            existing.NameAr = project.NameAr;
            existing.DescriptionEn = project.DescriptionEn;
            existing.DescriptionAr = project.DescriptionAr;
            existing.ModifiedAt = DateTime.UtcNow;

            _projectRepo.Update(existing);
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            var project = await _projectRepo.GetByIdAsync(id);

            if (project is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            project.IsDeleted = true;
            _projectRepo.Update(project);

            return Result.Success();
        }
    }
}
