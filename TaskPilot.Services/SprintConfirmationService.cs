using Hangfire;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.BackgroundJobs;
using TaskPilot.Services.Interfaces;
namespace TaskPilot.Services
{
    public class SprintConfirmationService : ISprintConfirmationService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IRepository<Sprint> _sprintRepository;
        private readonly IUnitOfWork _unitOfWork;

        private readonly IBackgroundJobClient _backgroundJobClient;
        public SprintConfirmationService(
            IRepository<Project> projectRepository,
            IUserStoryRepository userStoryRepository,
            ITaskRepository taskRepository,
            IRepository<Sprint> sprintRepository,
            IUnitOfWork unitOfWork,
            IBackgroundJobClient backgroundJobClient)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _taskRepository = taskRepository;
            _sprintRepository = sprintRepository;
            _unitOfWork = unitOfWork;
            _backgroundJobClient = backgroundJobClient;
        }

        public async Task<Result<ConfirmSprintResult>> ConfirmAsync(
            Guid projectId,
            ConfirmSprintRequest request,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate
            if (request.UserStoryIds is null || !request.UserStoryIds.Any())
                return Result.Failure<ConfirmSprintResult>(SprintErrors.NoUserStoriesSelected);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project is null)
                return Result.Failure<ConfirmSprintResult>(CommonErrors.NotFound("Project"));

            // 2. Load the selected UserStories and validate they belong to
            //    this project AND are currently unassigned (SprintId == null).
            var userStories = await _userStoryRepository
                .GetByIdsAsync(request.UserStoryIds, cancellationToken);

            var invalidStories = userStories
                .Where(s => s.ProjectId != projectId || s.SprintId != null)
                .ToList();

            if (invalidStories.Any())
                return Result.Failure<ConfirmSprintResult>(CommonErrors.InvalidInput(
                    $"{invalidStories.Count} of the selected UserStories are " +
                    $"invalid — they either don't belong to this project or " +
                    $"are already assigned to another Sprint."));

            if (userStories.Count != request.UserStoryIds.Count)
                return Result.Failure<ConfirmSprintResult>(CommonErrors.NotFound("UserStory", "One or more UserStory IDs were not found."));

            // 3. Resolve dates
            var startDate = request.StartDate ?? DateTime.UtcNow.Date;
            var endDate = request.EndDate
                ?? startDate.AddDays(project.SprintDurationInDays);

            // 4. Transaction
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var sprint = new Sprint
                {
                    ProjectId = projectId,
                    TitleEn = request.TitleEn,
                    TitleAr = request.TitleAr,
                    SprintGoalEn = request.SprintGoalEn,
                    SprintGoalAr = request.SprintGoalAr,
                    StartDate = startDate,
                    EndDate = endDate,
                    //Status = SprintStatus.Active
                    Status = SprintStatus.Planned
                };

                await _sprintRepository.AddAsync(sprint);

                // Assign UserStories
                var taskCount = 0;
                foreach (var story in userStories)
                {
                    story.SprintId = sprint.Id;

                    // Assign all Tasks belonging to this UserStory automatically
                    var tasks = await _taskRepository
                        .GetByUserStoryIdAsync(story.Id, cancellationToken);

                    foreach (var task in tasks)
                    {
                        if (task.Status != TaskItemStatus.Done)
                        {
                            task.SprintId = sprint.Id;
                            taskCount++;
                        }
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                // Schedule only after committing, so Hangfire can never receive an ID
                // for a sprint that was rolled back.
                _backgroundJobClient.Schedule<SprintCompletionJob>(
                    job => job.ExecuteAsync(sprint.Id),
                    new DateTimeOffset(DateTime.SpecifyKind(sprint.EndDate, DateTimeKind.Utc)));

                var confirmResult = new ConfirmSprintResult
                {
                    SprintId = sprint.Id,
                    ProjectId = projectId,
                    TitleEn = sprint.TitleEn,
                    StartDate = sprint.StartDate,
                    EndDate = sprint.EndDate,
                    UserStoriesAssigned = userStories.Count,
                    TasksAssigned = taskCount
                };
                return Result.Success(confirmResult);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<ConfirmSprintResult>(CommonErrors.ServerError(ex.Message));
            }
        }
    }
}
