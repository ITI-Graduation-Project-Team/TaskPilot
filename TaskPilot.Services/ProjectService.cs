using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.DTOs.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskPilot.Models.Enums;
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
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<ProjectService> _logger;
        private readonly ICurrentUserService _currentUserService;

        public ProjectService(
            IRepository<Project> projectRepo,
            IRepository<Company> companyRepo,
            IRepository<ProjectManager> managerRepo,
            ILocalizationService localizationService,
            ILogger<ProjectService> logger,
            ICurrentUserService currentUserService)
        {
            _projectRepo = projectRepo;
            _companyRepo = companyRepo;
            _managerRepo = managerRepo;
            _localizationService = localizationService;
            _logger = logger;
            _currentUserService = currentUserService;
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
                    ProjectType = p.ProjectType,
                    status = p.Status,
                    SetupStatus = p.SetupState == null
                        ? (p.UserStories.Any(us => !us.IsDeleted) ? ProjectSetupOverallStatus.Ready : ProjectSetupOverallStatus.NeedsTechStack)
                        : p.SetupState.WbsStatus == BackgroundSetupStatus.Failed || p.SetupState.TechStackStatus == TechStackSetupStatus.Failed
                            ? ProjectSetupOverallStatus.Failed
                            : p.SetupState.WbsStatus == BackgroundSetupStatus.Succeeded
                                ? (p.SetupState.SkillsStatus == BackgroundSetupStatus.Succeeded && p.SetupState.TasksSkipped == 0 ? ProjectSetupOverallStatus.Ready
                                    : p.SetupState.SkillsStatus == BackgroundSetupStatus.Failed || p.SetupState.SkillsStatus == BackgroundSetupStatus.PartiallySucceeded ? ProjectSetupOverallStatus.ReadyWithWarnings
                                    : ProjectSetupOverallStatus.EnrichingSkills)
                                : p.SetupState.WbsStatus == BackgroundSetupStatus.Running ? ProjectSetupOverallStatus.WbsGenerating
                                : p.SetupState.WbsStatus == BackgroundSetupStatus.Queued ? ProjectSetupOverallStatus.WbsQueued
                                : p.SetupState.TechStackStatus == TechStackSetupStatus.Confirmed ? ProjectSetupOverallStatus.ReadyForWbs
                                : ProjectSetupOverallStatus.NeedsTechStack
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
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId,
                    status = p.Status,
                    SetupStatus = p.SetupState != null && (p.SetupState.WbsStatus == BackgroundSetupStatus.Failed || p.SetupState.TechStackStatus == TechStackSetupStatus.Failed)
                        ? ProjectSetupOverallStatus.Failed
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Succeeded
                            ? ProjectSetupOverallStatus.Ready
                            : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Running
                                ? ProjectSetupOverallStatus.WbsGenerating
                                : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Queued
                                    ? ProjectSetupOverallStatus.WbsQueued
                                    : p.SetupState != null && p.SetupState.TechStackStatus == TechStackSetupStatus.Confirmed
                                        ? ProjectSetupOverallStatus.ReadyForWbs
                                        : ProjectSetupOverallStatus.NeedsTechStack
                })
                .ToListAsync();

            return Result.Success(projects);
        }

        public async Task<Result<IEnumerable<ProjectDto>>> GetByCompanyIdAsync(Guid companyId)
        {
            var companyExists = await _companyRepo.AnyAsync(c => c.Id == companyId);

            if (!companyExists)
                return Result.Failure<IEnumerable<ProjectDto>>(CompanyErrors.NotFound);

            var userId = _currentUserService.UserId;
            if (userId == null)
                return Result.Failure<IEnumerable<ProjectDto>>(CommonErrors.Unauthorized());

            bool isArabic = _localizationService.CurrentLanguage == "ar";

            var projects = await _projectRepo.GetQueryable()
                .Where(p => p.CompanyId == companyId && p.ManagerId == userId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId,
                    status = p.Status,
                    SetupStatus = p.SetupState != null && (p.SetupState.WbsStatus == BackgroundSetupStatus.Failed || p.SetupState.TechStackStatus == TechStackSetupStatus.Failed)
                        ? ProjectSetupOverallStatus.Failed
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Succeeded
                            ? ProjectSetupOverallStatus.Ready
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Running
                            ? ProjectSetupOverallStatus.WbsGenerating
                            : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Queued
                                ? ProjectSetupOverallStatus.WbsQueued
                                : p.SetupState != null && p.SetupState.TechStackStatus == TechStackSetupStatus.Confirmed
                                    ? ProjectSetupOverallStatus.ReadyForWbs
                                    : ProjectSetupOverallStatus.NeedsTechStack,
                    TeamSize = p.ProjectEmployees.Count,
                    TotalUserStories = p.UserStories.Count,
                    CompletedSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Completed),
                    ActiveSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Active)
                })
                .ToListAsync();

            return Result.Success(projects.AsEnumerable());
        }

        public async Task<Result<IEnumerable<ProjectDto>>> GetProjectsByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        {
            if (employeeId == Guid.Empty)
                return Result.Failure<IEnumerable<ProjectDto>>(CommonErrors.InvalidInput("Employee ID cannot be empty."));

            var isArabic = _localizationService.CurrentLanguage == "ar";

            var projects = await _projectRepo.GetQueryable()
                .Where(p => p.ProjectEmployees.Any(pe => pe.EmployeeId == employeeId) && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId,
                    TechStack = p.TechStack,
                    PlatformTargets = p.PlatformTargets,
                    ProjectType = p.ProjectType,
                    status = p.Status,
                    SetupStatus = p.SetupState != null && (p.SetupState.WbsStatus == BackgroundSetupStatus.Failed || p.SetupState.TechStackStatus == TechStackSetupStatus.Failed)
                        ? ProjectSetupOverallStatus.Failed
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Succeeded
                            ? ProjectSetupOverallStatus.Ready
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Running
                            ? ProjectSetupOverallStatus.WbsGenerating
                            : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Queued
                                ? ProjectSetupOverallStatus.WbsQueued
                                : p.SetupState != null && p.SetupState.TechStackStatus == TechStackSetupStatus.Confirmed
                                    ? ProjectSetupOverallStatus.ReadyForWbs
                                    : ProjectSetupOverallStatus.NeedsTechStack,
                    TeamSize = p.ProjectEmployees.Count,
                    TotalUserStories = p.UserStories.Count,
                    CompletedSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Completed),
                    ActiveSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Active)
                })
                .ToListAsync(cancellationToken);

            return Result.Success(projects.AsEnumerable());
        }

        public async Task<Result<PagedResult<ProjectDto>>> GetProjectsByCompanyIdPagedAsync(Guid companyId, int page, int pageSize, string? statusFilter = null, string? searchQuery = null, CancellationToken cancellationToken = default)
        {
            var companyExists = await _companyRepo.AnyAsync(c => c.Id == companyId);
            if (!companyExists)
                return Result.Failure<PagedResult<ProjectDto>>(CompanyErrors.NotFound);

            var userId = _currentUserService.UserId;
            if (userId == null)
                return Result.Failure<PagedResult<ProjectDto>>(CommonErrors.Unauthorized());

            var isArabic = _localizationService.CurrentLanguage == "ar";

            var query = _projectRepo.GetQueryable()
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId && p.ManagerId == userId && !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (statusFilter.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Draft);
                }
                else if (Enum.TryParse<ProjectStatus>(statusFilter, true, out var parsedStatus))
                {
                    query = query.Where(p => p.Status == parsedStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var lowerQuery = searchQuery.ToLower();
                query = query.Where(p =>
                    (p.NameEn != null && p.NameEn.ToLower().Contains(lowerQuery)) ||
                    (p.NameAr != null && p.NameAr.ToLower().Contains(lowerQuery)) ||
                    (p.DescriptionEn != null && p.DescriptionEn.ToLower().Contains(lowerQuery)) ||
                    (p.DescriptionAr != null && p.DescriptionAr.ToLower().Contains(lowerQuery)));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId,
                    TechStack = p.TechStack,
                    PlatformTargets = p.PlatformTargets,
                    ProjectType = p.ProjectType,
                    status = p.Status,
                    SetupStatus = p.SetupState != null && (p.SetupState.WbsStatus == BackgroundSetupStatus.Failed || p.SetupState.TechStackStatus == TechStackSetupStatus.Failed)
                        ? ProjectSetupOverallStatus.Failed
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Succeeded
                            ? ProjectSetupOverallStatus.Ready
                        : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Running
                            ? ProjectSetupOverallStatus.WbsGenerating
                            : p.SetupState != null && p.SetupState.WbsStatus == BackgroundSetupStatus.Queued
                                ? ProjectSetupOverallStatus.WbsQueued
                                : p.SetupState != null && p.SetupState.TechStackStatus == TechStackSetupStatus.Confirmed
                                    ? ProjectSetupOverallStatus.ReadyForWbs
                                    : ProjectSetupOverallStatus.NeedsTechStack,
                    TeamSize = p.ProjectEmployees.Count,
                    TotalUserStories = p.UserStories.Count(us => !us.IsDeleted),
                    CompletedSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Completed),
                    ActiveSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Active)
                })
                .ToListAsync(cancellationToken);

            var pagedResult = new PagedResult<ProjectDto>
            {
                Items = projects,
                TotalItems = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                HasNextPage = page * pageSize < totalCount,
                HasPreviousPage = page > 1
            };
            return Result.Success(pagedResult);
        }

        public async Task<Result<PagedResult<ProjectDto>>> GetProjectsByEmployeeIdPagedAsync(Guid employeeId, int page, int pageSize, string? statusFilter = null, string? searchQuery = null, CancellationToken cancellationToken = default)
        {
            var isArabic = _localizationService.CurrentLanguage == "ar";

            var query = _projectRepo.GetQueryable()
                .Where(p => p.ProjectEmployees.Any(pe => pe.EmployeeId == employeeId) && !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (statusFilter.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Draft);
                }
                else if (Enum.TryParse<ProjectStatus>(statusFilter, true, out var parsedStatus))
                {
                    query = query.Where(p => p.Status == parsedStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var lowerQuery = searchQuery.ToLower();
                query = query.Where(p =>
                    (p.NameEn != null && p.NameEn.ToLower().Contains(lowerQuery)) ||
                    (p.NameAr != null && p.NameAr.ToLower().Contains(lowerQuery)) ||
                    (p.DescriptionEn != null && p.DescriptionEn.ToLower().Contains(lowerQuery)) ||
                    (p.DescriptionAr != null && p.DescriptionAr.ToLower().Contains(lowerQuery)));
            }

            // Count on the filtered base query — no ORDER BY in the count SQL.
            var totalCount = await query.CountAsync(cancellationToken);

            var projects = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = isArabic ? p.NameAr : p.NameEn,
                    Description = isArabic ? p.DescriptionAr : p.DescriptionEn,
                    CompanyId = p.CompanyId,
                    ManagerId = p.ManagerId,
                    TechStack = p.TechStack,
                    PlatformTargets = p.PlatformTargets,
                    ProjectType = p.ProjectType,
                    status = p.Status,
                    TeamSize = p.ProjectEmployees.Count,
                    TotalUserStories = p.UserStories.Count(us => !us.IsDeleted),
                    CompletedSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Completed),
                    ActiveSprintsCount = p.Sprints.Count(s => s.Status == SprintStatus.Active)
                })
                .ToListAsync(cancellationToken);

            var pagedResult = new PagedResult<ProjectDto>
            {
                Items = projects,
                TotalItems = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                HasNextPage = page * pageSize < totalCount,
                HasPreviousPage = page > 1
            };
            return Result.Success(pagedResult);
        }

        public async Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto)
        {
            var userId = _currentUserService.UserId;
            if (userId == null)
                return Result.Failure<ProjectDto>(CommonErrors.Unauthorized());

            var managerExists = await _managerRepo.AnyAsync(pm => pm.Id == userId.Value);

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
                ManagerId = userId.Value,
                CompanyId = dto.CompanyId
                ,
                SetupState = new ProjectSetupState()
            };

            await _projectRepo.AddAsync(project);

            bool isArabic = _localizationService.CurrentLanguage == "ar";
            var resultDto = new ProjectDto
            {
                Id = project.Id,
                Name = isArabic ? project.NameAr : project.NameEn,
                Description = isArabic ? project.DescriptionAr : project.DescriptionEn,
                CompanyId = project.CompanyId,
                ManagerId = project.ManagerId,
                status = project.Status
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

        public async Task<Result<ProjectStatusDto>> GetStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<ProjectStatusDto>(ProjectErrors.InvalidProjectId);

            var project = await _projectRepo.GetQueryable()
                .Where(p => p.Id == projectId && !p.IsDeleted)
                .Select(p => new { p.Id, p.Status })
                .FirstOrDefaultAsync(cancellationToken);

            if (project == null)
                return Result.Failure<ProjectStatusDto>(ProjectErrors.NotFound);

            return Result.Success(new ProjectStatusDto
            {
                ProjectId = project.Id,
                Status = project.Status
            });
        }

        public async Task<Result<ProjectStatusDto>> UpdateStatusAsync(Guid projectId, ProjectStatusUpdateRequest request, string userId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<ProjectStatusDto>(ProjectErrors.InvalidProjectId);

            var project = await _projectRepo.GetByIdAsync(projectId);

            if (project == null)
                return Result.Failure<ProjectStatusDto>(ProjectErrors.NotFound);

            if (project.Status == request.Status)
                return Result.Success(new ProjectStatusDto { ProjectId = project.Id, Status = project.Status });

            var availableTransitions = GetAllowedTransitions(project.Status);

            if (!availableTransitions.Contains(request.Status))
                return Result.Failure<ProjectStatusDto>(ProjectErrors.InvalidStatusTransition);

            var oldStatus = project.Status;
            project.Status = request.Status;

            _projectRepo.Update(project);

            _logger.LogInformation("Project status updated. ProjectId: {ProjectId}, OldStatus: {OldStatus}, NewStatus: {NewStatus}, UserId: {UserId}, Transition: {Transition}",
                projectId, oldStatus.ToString(), project.Status.ToString(), userId, $"{oldStatus}->{project.Status}");

            return Result.Success(new ProjectStatusDto
            {
                ProjectId = project.Id,
                Status = project.Status
            });
        }

        public async Task<Result<List<ProjectStatusTransitionDto>>> GetAvailableTransitionsAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
                return Result.Failure<List<ProjectStatusTransitionDto>>(ProjectErrors.InvalidProjectId);

            var project = await _projectRepo.GetQueryable()
                .Where(p => p.Id == projectId && !p.IsDeleted)
                .Select(p => new { p.Status })
                .FirstOrDefaultAsync(cancellationToken);

            if (project == null)
                return Result.Failure<List<ProjectStatusTransitionDto>>(ProjectErrors.NotFound);

            var transitions = GetAllowedTransitions(project.Status)
                .Select(t => new ProjectStatusTransitionDto
                {
                    FromStatus = project.Status,
                    ToStatus = t
                })
                .ToList();

            return Result.Success(transitions);
        }


        private List<ProjectStatus> GetAllowedTransitions(ProjectStatus currentStatus)
        {
            return currentStatus switch
            {
                ProjectStatus.Draft => new List<ProjectStatus> { ProjectStatus.Active, ProjectStatus.Archived },
                ProjectStatus.Active => new List<ProjectStatus> { ProjectStatus.Completed, ProjectStatus.Archived, ProjectStatus.Draft },
                ProjectStatus.Completed => new List<ProjectStatus> { ProjectStatus.Archived, ProjectStatus.Active, ProjectStatus.Draft },
                ProjectStatus.Archived => new List<ProjectStatus> { ProjectStatus.Active, ProjectStatus.Draft },
                _ => new List<ProjectStatus>()
            };
        }
    }
}
