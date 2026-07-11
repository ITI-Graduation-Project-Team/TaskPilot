using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using Microsoft.EntityFrameworkCore;

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
        private readonly ILocalizationService _localizationService;

        public ProjectService(
            IRepository<Project> projectRepo, 
            IRepository<Company> companyRepo,
            IRepository<ProjectManager> managerRepo,
            ILocalizationService localizationService)
        {
            _projectRepo = projectRepo;
            _companyRepo = companyRepo;
            _managerRepo = managerRepo;
            _localizationService = localizationService;
        }

        public async Task<Result<ProjectDto>> GetByIdAsync(Guid id)
        {
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var project = await _projectRepo.GetQueryable()
                .Where(p => p.Id == id && !p.IsDeleted)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId,
                    TechStack = p.TechStack,
                    PlatformTargets = p.PlatformTargets,
                    ProjectType = p.ProjectType
                })
                .FirstOrDefaultAsync();

            if (project is null)
                return Result.Failure<ProjectDto>(ProjectErrors.NotFound);

            return Result.Success(project);
        }

        public async Task<Result<List<ProjectDto>>> GetAllAsync()
        {
            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var projects = await _projectRepo.GetQueryable()
                .Where(p => !p.IsDeleted)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId
                })
                .ToListAsync();

            return Result.Success(projects);
        }

        public async Task<Result<IEnumerable<ProjectDto>>> GetByCompanyIdAsync(Guid companyId)
        {
            var companyExists = await _companyRepo.AnyAsync(c => c.Id == companyId);

            if (!companyExists)
                return Result.Failure<IEnumerable<ProjectDto>>(CompanyErrors.NotFound);

            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var projects = await _projectRepo.GetQueryable()
                .Where(p => p.CompanyId == companyId && !p.IsDeleted)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId
                })
                .ToListAsync();

            return Result.Success(projects.AsEnumerable());
        }

        public async Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto)
        {
            var managerExists = await _managerRepo.AnyAsync(pm => pm.Id == dto.ManagerId);

            if (!managerExists)
                return Result.Failure<ProjectDto>(UserErrors.ProjectManagerNotFound);

            var companyExists = await _companyRepo.AnyAsync(c => c.Id == dto.CompanyId);

            if (!companyExists)
                return Result.Failure<ProjectDto>(CompanyErrors.NotFound);

            var project = new Project
            {
                NameEn = dto.NameEn,
                NameAr = dto.NameAr,
                DescriptionEn = dto.DescriptionEn,
                DescriptionAr = dto.DescriptionAr,
                ManagerId = dto.ManagerId,
                CompanyId = dto.CompanyId
            };

            await _projectRepo.AddAsync(project);

            bool isArabic = _localizationService.CurrentLanguage == "ar";
            var resultDto = new ProjectDto
            {
                Id = project.Id,
                Name = isArabic ? project.NameAr : project.NameEn,
                Description = isArabic ? project.DescriptionAr : project.DescriptionEn,
                CompanyId = project.CompanyId,
                ManagerId = project.ManagerId
            };

            return Result.Success(resultDto);
        }

        public async Task<Result> UpdateAsync(UpdateProjectDto dto)
        {
            var existing = await _projectRepo.GetByIdAsync(dto.Id);

            if (existing is null)
                return Result.Failure(ProjectErrors.NotFound);

            existing.NameEn = dto.NameEn;
            existing.NameAr = dto.NameAr;
            existing.DescriptionEn = dto.DescriptionEn;
            existing.DescriptionAr = dto.DescriptionAr;
            existing.ModifiedAt = DateTime.UtcNow;

            _projectRepo.Update(existing);
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            var project = await _projectRepo.GetByIdAsync(id);

            if (project is null)
                return Result.Failure(ProjectErrors.NotFound);

            project.IsDeleted = true;
            _projectRepo.Update(project);

            return Result.Success();
        }
    }
}
