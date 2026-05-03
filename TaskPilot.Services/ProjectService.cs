using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Services
{
    /// <summary>
    /// Contains all business logic for Project operations.
    /// Accesses data exclusively through IUnitOfWork — never touches DbContext directly.
    /// Does NOT call SaveChangesAsync — that is the controller's responsibility via IUnitOfWork.
    /// </summary>
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;

        public ProjectService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result<Project>> GetByIdAsync(Guid id)
        {
            var project = await _context.Projects
                .Include(p => p.Manager)
                .Include(p => p.Company)
                .Include(p => p.Sprints)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project is null)
                return Result.Failure<Project>(CommonErrors.NotFound("Project"));

            return Result.Success(project);
        }

        public async Task<Result<List<Project>>> GetAllAsync()
        {
            var projects = await _context.Projects
                .Include(p => p.Manager)
                .Include(p => p.Company)
                .ToListAsync();

            return Result.Success(projects);
        }

        public async Task<Result<IEnumerable<Project>>> GetByCompanyIdAsync(Guid companyId)
        {
            var companyExists = await _context  .Companies
                .AnyAsync(c => c.Id == companyId);

            if (!companyExists)
                return Result.Failure<IEnumerable<Project>>(CommonErrors.NotFound("Company"));

            var projects = await _context.Projects
                .Where(p => p.CompanyId == companyId)
                .Include(p => p.Manager)
                .ToListAsync();

            return Result.Success(projects.AsEnumerable());
        }

        public async Task<Result<Project>> CreateAsync(Project project)
        {
            var managerExists = await _context.Users
                .OfType<ProjectManager>()
                .AnyAsync(pm => pm.Id == project.ManagerId);

            if (!managerExists)
                return Result.Failure<Project>(CommonErrors.NotFound("Project Manager"));

            var companyExists = await _context.Companies
                .AnyAsync(c => c.Id == project.CompanyId);

            if (!companyExists)
                return Result.Failure<Project>(CommonErrors.NotFound("Company"));

            await _context.Projects.AddAsync(project);
            return Result.Success(project);
        }

        public async Task<Result> UpdateAsync(Project project)
        {
            var existing = await _context.Projects.FindAsync(project.Id);

            if (existing is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            existing.NameEn = project.NameEn;
            existing.NameAr = project.NameAr;
            existing.DescriptionEn = project.DescriptionEn;
            existing.DescriptionAr = project.DescriptionAr;
            existing.ModifiedAt = DateTime.UtcNow;

            _context.Projects.Update(existing);
            return Result.Success();
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            project.IsDeleted = true;

            return Result.Success();
        }
    }
}
