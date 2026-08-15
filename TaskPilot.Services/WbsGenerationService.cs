using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.DTOs;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Persistence.Interfaces;
using Microsoft.Extensions.Logging;
namespace TaskPilot.Services
{
    public class WbsGenerationService : IWbsGenerationService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<UserStory> _userStoryRepository;
        private readonly ISkillRepository _skillRepository;
        private readonly WBSGenerationAgent _wbsAgent;
        private readonly IWbsPersistenceService _wbsPersistenceService;
        private readonly IProjectChatService _projectChatService;
        private readonly IRequirementSessionStore _sessionStore;
        private readonly ILogger<WbsGenerationService> _logger;
        private readonly TaskPilot.AI.Services.Interfaces.ITelemetryAccumulator _telemetry;

        public WbsGenerationService(
            IRepository<Project> projectRepository,
            IRepository<UserStory> userStoryRepository,
            ISkillRepository skillRepository,
            WBSGenerationAgent wbsAgent,
            IWbsPersistenceService wbsPersistenceService,
            IProjectChatService projectChatService,
            IRequirementSessionStore sessionStore,
            ILogger<WbsGenerationService> logger,
            TaskPilot.AI.Services.Interfaces.ITelemetryAccumulator telemetry)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _skillRepository = skillRepository;
            _wbsAgent = wbsAgent;
            _wbsPersistenceService = wbsPersistenceService;
            _projectChatService = projectChatService;
            _sessionStore = sessionStore;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task<Result<WbsPersistenceResult>> GenerateAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project is null)
                return Result<WbsPersistenceResult>.Failure(CommonErrors.NotFound("Project"));

            if (project.RequirementsSnapshot is null)
                return Result<WbsPersistenceResult>.Failure(CommonErrors.InvalidInput("Project has no RequirementsSnapshot. Complete requirement finalization first."));

            var hasUserStories = await _userStoryRepository.AnyAsync(us => us.ProjectId == projectId && !us.IsDeleted);

            if (hasUserStories)
            {
                return Result<WbsPersistenceResult>.Failure(new Error(
                    "BACKLOG_ALREADY_EXISTS",
                     ErrorType.Conflict,
                    "This project already contains a generated backlog. Review the existing backlog or use the Regenerate endpoint to replace it."
                   ));
            }

            var skills = await _skillRepository.GetProjectSkillSummaryAsync(projectId, cancellationToken);
            var availableSkills = skills.Select(skill => skill.SkillName).ToList();

            var wbs = await _wbsAgent.GenerateAsync(
                project.RequirementsSnapshot,
                project.TechStack,
                project.PlatformTargets,
                project.ProjectType,
                availableSkills,
                project.RequirementsSessionId ?? Guid.Empty,
                cancellationToken);

            var result = await _wbsPersistenceService.PersistAsync(projectId, wbs, cancellationToken);
            
            if (result.IsSuccess && project.RequirementsSessionId.HasValue)
            {
                try
                {
                    var reqSession = await _sessionStore.GetAsync(project.RequirementsSessionId.Value, cancellationToken);
                    if (reqSession != null && reqSession.ConversationHistory != null)
                    {
                        var messagesToAppend = new System.Collections.Generic.List<(string, string)>();
                        foreach (var msg in reqSession.ConversationHistory)
                        {
                            if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                            {
                                messagesToAppend.Add(("User", msg.Message));
                            }
                            else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                            {
                                messagesToAppend.Add(("Assistant", msg.Message));
                            }
                        }

                        if (messagesToAppend.Count > 0)
                        {
                            await _projectChatService.AppendMessagesAsync(projectId, messagesToAppend, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist chat history for project {ProjectId}. The backlog was still generated successfully.", projectId);
                }
            }

            _logger.LogInformation("Pipeline run completed: ProjectId={ProjectId} TotalInputTokens={InputTokens} TotalOutputTokens={OutputTokens} TotalElapsedMs={ElapsedMs}", projectId, _telemetry.TotalInputTokens, _telemetry.TotalOutputTokens, _telemetry.TotalElapsedMs);
            _telemetry.Reset();

            return result;
        }
    }
}
