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
using TaskPilot.AI.Services.Requirements;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using Hangfire;
using TaskPilot.Services.BackgroundJobs;
using TaskPilot.AI.Agents.Requirements;

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
        private readonly IRequirementReadinessEvaluator _readinessEvaluator;
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly RequirementsBuilderAgent _requirementsBuilder;

        public RequirementFinalizationService(
            IRequirementSessionStore sessionStore,
            IUnitOfWork unitOfWork,
            IRepository<Project> projectRepository,
            IRepository<Company> companyRepository,
            UserManager<User> userManager,
            ILogger<RequirementFinalizationService> logger,
            IRequirementReadinessEvaluator readinessEvaluator,
            IBackgroundJobClient backgroundJobs,
            RequirementsBuilderAgent requirementsBuilder)
        {
            _sessionStore = sessionStore;
            _unitOfWork = unitOfWork;
            _projectRepository = projectRepository;
            _companyRepository = companyRepository;
            _userManager = userManager;
            _logger = logger;
            _readinessEvaluator = readinessEvaluator;
            _backgroundJobs = backgroundJobs;
            _requirementsBuilder = requirementsBuilder;
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

            // Preserve the original confirmation threshold used by the UI.
            // Finalize is also responsible for building a missing requirements
            // snapshot, including for sessions created before async setup existed.
            var gateReport = session.RequirementCompletenessReport;
            if (gateReport == null || gateReport.OverallCompleteness == 0)
            {
                gateReport = _readinessEvaluator.Evaluate(session);
                session.RequirementCompletenessReport = gateReport;
            }

            if (gateReport.MeetsConfirmationThreshold())
            {
                gateReport.ReadyForFinalization = true;
                session.Status = RequirementSessionStatus.Planning;

                if (session.CompletenessReport != null)
                {
                    session.CompletenessReport.ReadyForPlanning = true;
                }

                if (session.QuestionPool != null)
                {
                    foreach (var question in session.QuestionPool.Where(q => !q.IsAnswered))
                    {
                        question.IsAnswered = true;
                        question.AnsweredAt = DateTime.UtcNow;
                        question.Answer ??= "Accepted during requirements finalization.";
                        question.AnsweredFromSource ??= "System";
                    }
                }
            }

            // This is the legacy behavior: pressing Confirm prepares the final
            // snapshot on demand instead of returning REQUIREMENTS_NOT_READY.
            session.FinalRequirements ??= await _requirementsBuilder.BuildAsync(session, cancellationToken);

            if (session.FinalRequirements == null)
            {
                return Result.Failure<FinalizeRequirementsResponse>(
                    CommonErrors.Conflict("REQUIREMENTS_NOT_READY", "Final requirements are still being prepared. Complete the requirements flow and retry."));
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

            // Use the stored completeness report for the gate check.
            // RequirementDiscoveryOrchestrator already stores the authoritative deterministic score
            // on every chat turn. Only recompute if the session has never been through a chat turn
            // (i.e., the report is null or has a zero score).
            if (!gateReport.ReadyForFinalization)
            {
                var blocks = gateReport.BlockingFactors.Any() 
                    ? string.Join(" ", gateReport.BlockingFactors) 
                    : "Requirements need further clarification.";
                return Result.Failure<FinalizeRequirementsResponse>(CommonErrors.InvalidInput($"Session is not ready for planning yet. Completeness is {gateReport.OverallCompleteness}%. {blocks}"));
            }
            else
            {
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
                DescriptionEn = request.DescriptionEn,
                DescriptionAr = request.DescriptionAr,
                Status = ProjectStatus.Draft,
                SprintDurationInDays = request.SprintDurationInDays,
                TargetSprintHours = request.TargetSprintHours,
                RequirementsSnapshot = snapshot,
                RequirementsSessionId = sessionId,
                DocumentIds = session.Knowledge.DocumentIds.ToList()
                ,SetupState = new ProjectSetupState()
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

                if (project.DocumentIds.Count > 0)
                    _backgroundJobs.Enqueue<ProjectKnowledgePromotionJob>(job => job.ExecuteAsync(project.Id));
                
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
                RequirementsFinalized = true,
                SetupStatus = ProjectSetupOverallStatus.NeedsTechStack.ToString()
            };
            return Result.Success(response);
        }
    }
}
