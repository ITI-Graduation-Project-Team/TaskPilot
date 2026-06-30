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

namespace TaskPilot.Services
{
    public class WbsGenerationService : IWbsGenerationService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<UserStory> _userStoryRepository;
        private readonly WBSGenerationAgent _wbsAgent;
        private readonly IWbsPersistenceService _wbsPersistenceService;

        public WbsGenerationService(
            IRepository<Project> projectRepository,
            IRepository<UserStory> userStoryRepository,
            WBSGenerationAgent wbsAgent,
            IWbsPersistenceService wbsPersistenceService)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _wbsAgent = wbsAgent;
            _wbsPersistenceService = wbsPersistenceService;
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

            var wbs = await _wbsAgent.GenerateAsync(
                project.RequirementsSnapshot,
                project.TechStack,
                project.PlatformTargets,
                project.ProjectType,
                cancellationToken);

            var result = await _wbsPersistenceService.PersistAsync(projectId, wbs, cancellationToken);

            if (!result.Success)
                return Result<WbsPersistenceResult>.Failure(CommonErrors.OperationFailed(result.Error));

            return Result<WbsPersistenceResult>.Success(result);
        }
    }
}
