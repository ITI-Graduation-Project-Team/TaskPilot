using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Services
{
    public class BacklogRegenerationService : IBacklogRegenerationService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IRepository<UserStory> _userStoryGenericRepository;
        private readonly IRepository<Skill> _skillRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly WBSGenerationAgent _wbsGenerationAgent;
        private readonly IWbsPersistenceService _wbsPersistenceService;
        private readonly ILogger<BacklogRegenerationService> _logger;

        public BacklogRegenerationService(
            IRepository<Project> projectRepository,
            IUserStoryRepository userStoryRepository,
            IRepository<TaskItem> taskRepository,
            IRepository<UserStory> userStoryGenericRepository,
            IRepository<Skill> skillRepository,
            IUnitOfWork unitOfWork,
            WBSGenerationAgent wbsGenerationAgent,
            IWbsPersistenceService wbsPersistenceService,
            ILogger<BacklogRegenerationService> logger)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _taskRepository = taskRepository;
            _userStoryGenericRepository = userStoryGenericRepository;
            _skillRepository = skillRepository;
            _unitOfWork = unitOfWork;
            _wbsGenerationAgent = wbsGenerationAgent;
            _wbsPersistenceService = wbsPersistenceService;
            _logger = logger;
        }

        public async Task<Result<RegenerationSummaryDto>> RegenerateBacklogAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Starting backlog regeneration for ProjectId: {ProjectId}", projectId);

            // Step 1: Load Project & RequirementsSnapshot
            var project = await _projectRepository.GetByIdAsync(projectId, p => p.RequirementsSnapshot);
            if (project == null)
            {
                return Result.Failure<RegenerationSummaryDto>(CommonErrors.NotFound("Project"));
            }

            // Step 2: Validate RequirementsSnapshot
            if (project.RequirementsSnapshot == null)
            {
                return Result.Failure<RegenerationSummaryDto>(CommonErrors.InvalidInput("Project has no RequirementsSnapshot."));
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // Step 3: Delete existing backlog
                var userStories = await _userStoryRepository.GetByProjectIdAsync(projectId, cancellationToken);
                var deletedUserStoriesCount = userStories.Count;
                
                var tasks = userStories.SelectMany(u => u.Tasks).ToList();
                var deletedTasksCount = tasks.Count;

                if (tasks.Any())
                {
                    _taskRepository.DeleteRange(tasks);
                }

                if (userStories.Any())
                {
                    _userStoryGenericRepository.DeleteRange(userStories);
                }

                // Explicitly save the deletions before regenerating, 
                // so persistence service doesn't face conflicts or duplicate tracking.
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var skills = await _skillRepository.GetAllAsync();
                var availableSkills = System.Linq.Enumerable.Select(skills, s => s.Name).ToList();

                // Step 4: Invoke WBSGenerationAgent
                var generatedWbs = await _wbsGenerationAgent.GenerateAsync(
                    project.RequirementsSnapshot,
                    project.TechStack,
                    project.PlatformTargets,
                    project.ProjectType,
                    availableSkills,
                    cancellationToken);

                // Step 5: Persist newly generated backlog
                var persistenceResult = await _wbsPersistenceService.PersistAsync(projectId, generatedWbs, cancellationToken);

                if (persistenceResult.IsFailure)
                {
                    return Result.Failure<RegenerationSummaryDto>(persistenceResult.Error);
                }

                var persistenceVal = persistenceResult.Value;

                // Explicitly save all changes made by the persistence service
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                stopwatch.Stop();
                _logger.LogInformation("Successfully regenerated backlog for ProjectId: {ProjectId}. " +
                    "Deleted UserStories: {DeletedUS}, Deleted Tasks: {DeletedTasks}. " +
                    "Generated UserStories: {GeneratedUS}, Generated Tasks: {GeneratedTasks}. Execution Time: {ExecutionTimeMs}ms",
                    projectId, deletedUserStoriesCount, deletedTasksCount, 
                    persistenceVal.UserStoriesCreated, persistenceVal.TasksCreated, stopwatch.ElapsedMilliseconds);

                // Step 6: Return summary
                var summary = new RegenerationSummaryDto
                {
                    ProjectId = projectId,
                    DeletedUserStories = deletedUserStoriesCount,
                    DeletedTasks = deletedTasksCount,
                    GeneratedUserStories = persistenceVal.UserStoriesCreated,
                    GeneratedTasks = persistenceVal.TasksCreated,
                    Message = "Backlog regenerated successfully."
                };
                return Result.Success(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to regenerate backlog for ProjectId: {ProjectId}. Rolling back transaction.", projectId);
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<RegenerationSummaryDto>(CommonErrors.ServerError(ex.Message));
            }
        }
    }
}
