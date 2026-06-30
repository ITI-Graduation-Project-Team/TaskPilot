using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
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

        public SprintConfirmationService(
            IRepository<Project> projectRepository,
            IUserStoryRepository userStoryRepository,
            ITaskRepository taskRepository,
            IRepository<Sprint> sprintRepository,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _taskRepository = taskRepository;
            _sprintRepository = sprintRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ConfirmSprintResult> ConfirmAsync(
            Guid projectId,
            ConfirmSprintRequest request,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate
            if (request.UserStoryIds is null || !request.UserStoryIds.Any())
                throw new ArgumentException("At least one UserStory must be selected for the sprint.");

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project is null)
                throw new KeyNotFoundException("Project not found.");

            // 2. Load the selected UserStories and validate they belong to
            //    this project AND are currently unassigned (SprintId == null).
            //    This prevents accidentally re-assigning a story that already
            //    belongs to a different sprint.
            var userStories = await _userStoryRepository
                .GetByIdsAsync(request.UserStoryIds, cancellationToken);

            var invalidStories = userStories
                .Where(s => s.ProjectId != projectId || s.SprintId != null)
                .ToList();

            if (invalidStories.Any())
                throw new ArgumentException(
                    $"{invalidStories.Count} of the selected UserStories are " +
                    $"invalid — they either don't belong to this project or " +
                    $"are already assigned to another Sprint.");

            if (userStories.Count != request.UserStoryIds.Count)
                throw new ArgumentException("One or more UserStory IDs were not found.");

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
                        task.SprintId = sprint.Id;
                        taskCount++;
                    }
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new ConfirmSprintResult
                {
                    SprintId = sprint.Id,
                    ProjectId = projectId,
                    TitleEn = sprint.TitleEn,
                    StartDate = sprint.StartDate,
                    EndDate = sprint.EndDate,
                    UserStoriesAssigned = userStories.Count,
                    TasksAssigned = taskCount
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
