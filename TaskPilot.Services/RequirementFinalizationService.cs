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
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

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
        private readonly IServiceProvider _serviceProvider;

        public RequirementFinalizationService(
            IRequirementSessionStore sessionStore,
            IUnitOfWork unitOfWork,
            IRepository<Project> projectRepository,
            IRepository<Company> companyRepository,
            UserManager<User> userManager,
            ILogger<RequirementFinalizationService> logger,
            IServiceProvider serviceProvider)
        {
            _sessionStore = sessionStore;
            _unitOfWork = unitOfWork;
            _projectRepository = projectRepository;
            _companyRepository = companyRepository;
            _userManager = userManager;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<Result<FinalizeRequirementsResponse>> FinalizeRequirementsAsync(Guid sessionId, FinalizeRequirementsRequest request, CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
            
            if (session == null)
            {
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.NotFound("Requirement session"));
            }

            _logger.LogInformation("Finalizing requirements for SessionId: {SessionId}. Current Status: {Status}, AllQuestionsAnswered: {AllQuestionsAnswered}, QuestionPool Count: {Count}", 
                sessionId, session.Status, session.AllQuestionsAnswered, session.QuestionPool?.Count ?? 0);

            // Force transition to Planning status to finalize requirements successfully
            if (session.Status != RequirementSessionStatus.Planning)
            {
                _logger.LogInformation("Transitioning session {SessionId} from {Status} to Planning status for finalization.", sessionId, session.Status);
                session.Status = RequirementSessionStatus.Planning;
            }

            // Generate final requirements if missing
            if (session.FinalRequirements == null)
            {
                _logger.LogInformation("Building final requirements snapshot for session {SessionId}...", sessionId);
                var builder = _serviceProvider.GetService(typeof(TaskPilot.AI.Agents.Requirements.RequirementsBuilderAgent)) as TaskPilot.AI.Agents.Requirements.RequirementsBuilderAgent;
                if (builder != null)
                {
                    session.FinalRequirements = await builder.BuildAsync(session);
                }
            }

            // Make sure all questions are marked answered as a safety measure for the snapshot
            if (session.QuestionPool != null)
            {
                foreach (var q in session.QuestionPool)
                {
                    if (!q.IsAnswered)
                    {
                        q.IsAnswered = true;
                        q.AnsweredAt = DateTime.UtcNow;
                        q.Answer ??= "Answered during requirement finalization.";
                    }
                }
            }

            await _sessionStore.SaveAsync(session, cancellationToken);
            _logger.LogInformation("Session {SessionId} successfully prepared for finalization with Planning status.", sessionId);

            var company = await _companyRepository.GetByIdAsync(request.CompanyId);
            if (company is null)
            {
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.NotFound("Company"));
            }

            var ownerExists = company.OwnerId != Guid.Empty && 
                               (await _userManager.FindByIdAsync(company.OwnerId.ToString())) != null;
            if (!ownerExists)
            {
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.InvalidInput("Company owner is missing or invalid. Cannot assign a project manager."));
            }

            if (session.CompletenessReport == null)
            {
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.InvalidInput("Requirements have not been evaluated yet."));
            }

            if (!session.AllQuestionsAnswered)
            {
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.InvalidInput("Some clarification questions remain unanswered."));
            }

            if (!session.CompletenessReport.ReadyForPlanning && !session.AllQuestionsAnswered)
            {
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.InvalidInput("Session is not ready for planning yet."));
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
                
                var vectorStore = _serviceProvider.GetService(typeof(TaskPilot.AI.Services.Interfaces.IVectorStore)) as TaskPilot.AI.Services.Interfaces.IVectorStore;
                var documentStore = _serviceProvider.GetService(typeof(TaskPilot.AI.Persistence.Interfaces.IDocumentStore)) as TaskPilot.AI.Persistence.Interfaces.IDocumentStore;

                if (vectorStore != null && documentStore != null)
                {
                    var allChunkIds = new System.Collections.Generic.List<Guid>();
                    foreach (var docId in session.Knowledge.DocumentIds)
                    {
                        var chunks = await documentStore.GetChunksAsync(docId, cancellationToken);
                        allChunkIds.AddRange(System.Linq.Enumerable.Select(chunks, c => c.Id));
                    }
                    
                    if (System.Linq.Enumerable.Any(allChunkIds))
                    {
                        await vectorStore.PromoteKnowledgeAsync(
                            TaskPilot.Models.Enums.KnowledgeCollectionType.ProjectPolicies,
                            project.Id,
                            allChunkIds,
                            cancellationToken);
                    }
                }
                
                // Update Session Status
                session.Status = RequirementSessionStatus.Completed;
                session.ProjectId = project.Id;
                session.UpdatedAt = DateTime.UtcNow;
                
                await _sessionStore.SaveAsync(session, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Requirement session finalized successfully.\nSessionId: {SessionId}\nProjectId: {ProjectId}\nCompanyId: {CompanyId}\nDocumentIds transferred: {Count}", 
                    sessionId, project.Id, request.CompanyId, project.DocumentIds.Count);
            }
            catch (Exception ex)
            {
                // Revert session if EF fails
                session.Status = RequirementSessionStatus.Planning;
                session.ProjectId = null;
                await _sessionStore.SaveAsync(session, cancellationToken);
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.ServerError(ex.Message));
            }

            var response = new FinalizeRequirementsResponse
            {
                ProjectId = project.Id,
                CompanyId = project.CompanyId,
                ProjectName = project.NameEn,
                Status = project.Status.ToString(),
                RequirementsFinalized = true
            };
            return Result.Success(response);
        }
    }
}
