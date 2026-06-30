using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.Requirements;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Exceptions;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class RequirementFinalizationService : IRequirementFinalizationService
    {
        private readonly IRequirementSessionStore _sessionStore;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Company> _companyRepository;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<RequirementFinalizationService> _logger;

        public RequirementFinalizationService(
            IRequirementSessionStore sessionStore,
            IUnitOfWork unitOfWork,
            IRepository<Project> projectRepository,
            IRepository<Company> companyRepository,
            UserManager<User> userManager,
            ILogger<RequirementFinalizationService> logger)
        {
            _sessionStore = sessionStore;
            _unitOfWork = unitOfWork;
            _projectRepository = projectRepository;
            _companyRepository = companyRepository;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<FinalizeRequirementsResponse> FinalizeRequirementsAsync(Guid sessionId, FinalizeRequirementsRequest request, CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
            
            if (session == null)
            {
                throw new ArgumentException("Requirement session was not found.");
            }

            if (session.Status == RequirementSessionStatus.Completed)
            {
                throw new SessionAlreadyFinalizedException(session.ProjectId);
            }

            if (session.Status != RequirementSessionStatus.Planning)
            {
                throw new InvalidOperationException("Session must be in Planning status before finalization.");
            }

            var company = await _companyRepository.GetByIdAsync(request.CompanyId);
            if (company is null)
            {
                throw new ArgumentException("Company was not found.");
            }

            var ownerExists = company.OwnerId != Guid.Empty && 
                              (await _userManager.FindByIdAsync(company.OwnerId.ToString())) != null;
            if (!ownerExists)
            {
                throw new UnprocessableEntityException("Company owner is missing or invalid. Cannot assign a project manager.");
            }

            if (session.CompletenessReport == null)
            {
                throw new InvalidOperationException("Requirements have not been evaluated yet.");
            }

            if (!session.AllQuestionsAnswered)
            {
                throw new InvalidOperationException("Some clarification questions remain unanswered.");
            }

            if (!session.CompletenessReport.ReadyForPlanning)
            {
                throw new InvalidOperationException("Session is not ready for planning yet.");
            }

            // Create Requirements Snapshot
            var snapshot = new RequirementsSnapshot
            {
                BusinessRequirements = session.FinalRequirements?.BusinessRequirements ?? new(),
                TechnicalRequirements = session.FinalRequirements?.TechnicalRequirements ?? new(),
                Constraints = session.FinalRequirements?.Constraints ?? new(),
                Integrations = session.FinalRequirements?.Integrations ?? new(),
                ScaleRequirements = session.FinalRequirements?.ScaleRequirements ?? new()
            };
            
            _logger.LogInformation("Requirements snapshot created successfully.");

            // Create Project
            var project = new Project
            {
                CompanyId = company.Id,
                ManagerId = company.OwnerId,
                NameEn = request.ProjectNameEn,
                NameAr = request.ProjectNameAr ?? string.Empty,
                Status = ProjectStatus.Draft,
                SprintDurationInDays = request.SprintDurationInDays,
                TargetSprintHours = request.TargetSprintHours,
                RequirementsSnapshot = snapshot,
                DocumentIds = session.Knowledge.DocumentIds.ToList()
            };

            try
            {
                await _projectRepository.AddAsync(project);
                
                // Update Session Status
                session.Status = RequirementSessionStatus.Completed;
                session.ProjectId = project.Id;
                session.UpdatedAt = DateTime.UtcNow;
                
                await _sessionStore.SaveAsync(session, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Requirement session finalized successfully.\nSessionId: {SessionId}\nProjectId: {ProjectId}\nCompanyId: {CompanyId}\nDocumentIds transferred: {Count}", 
                    sessionId, project.Id, request.CompanyId, project.DocumentIds.Count);
            }
            catch
            {
                // Revert session if EF fails
                session.Status = RequirementSessionStatus.Planning;
                session.ProjectId = null;
                await _sessionStore.SaveAsync(session, cancellationToken);
                throw;
            }

            return new FinalizeRequirementsResponse
            {
                ProjectId = project.Id,
                CompanyId = project.CompanyId,
                ProjectName = project.NameEn,
                Status = project.Status.ToString(),
                RequirementsFinalized = true
            };
        }
    }
}
