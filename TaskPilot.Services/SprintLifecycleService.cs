using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SprintLifecycleService : ISprintLifecycleService
    {
        private readonly ISprintRepository _sprintRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly ILogger<SprintLifecycleService> _logger;

        public SprintLifecycleService(
            ISprintRepository sprintRepository,
            IRepository<Project> projectRepository,
            ILogger<SprintLifecycleService> logger)
        {
            _sprintRepository = sprintRepository;
            _projectRepository = projectRepository;
            _logger = logger;
        }

        public async Task<Result<SprintStatusDto>> StartSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty) return Result.Failure<SprintStatusDto>(SprintErrors.InvalidProject);
            if (sprintId == Guid.Empty) return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprint);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.ProjectNotFound);
            }

            var sprint = await _sprintRepository.GetByIdAsync(sprintId);
            if (sprint == null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotFound);
            }

            if (sprint.ProjectId != projectId)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintDoesNotBelongToProject);
            }

            if (sprint.Status == SprintStatus.Active)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyActive);
            }
            
            if (sprint.Status == SprintStatus.Completed)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyCompleted);
            }

            if (sprint.Status != SprintStatus.Planned)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprintStatus);
            }

            var activeSprint = await _sprintRepository.GetActiveSprintByProjectIdAsync(projectId, cancellationToken);
            if (activeSprint != null)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.AnotherSprintAlreadyActive);
            }

            sprint.Status = SprintStatus.Active;
            sprint.StartDate = DateTime.UtcNow;

            _logger.LogInformation("Sprint {SprintId} started for Project {ProjectId}", sprintId, projectId);

            return Result.Success(new SprintStatusDto
            {
                SprintId = sprint.Id,
                Status = sprint.Status.ToString()
            });
        }

        public async Task<Result<SprintStatusDto>> CompleteSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
    public sealed class SprintLifecycleService(
        IRepository<Sprint> sprintRepository,
        IUnitOfWork unitOfWork) : ISprintLifecycleService
    {
            if (projectId == Guid.Empty) return Result.Failure<SprintStatusDto>(SprintErrors.InvalidProject);
            if (sprintId == Guid.Empty) return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprint);

            var sprint = await _sprintRepository.GetSprintWithTasksAsync(sprintId, cancellationToken);
            if (sprint == null)
        public async Task<bool> EnsureCompletedIfDueAsync(Guid sprintId, CancellationToken cancellationToken = default)
        {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotFound);
            }
            var sprint = await sprintRepository.GetByIdAsync(sprintId);

            if (sprint.ProjectId != projectId)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintDoesNotBelongToProject);
            }
            if (sprint is null || sprint.IsDeleted || sprint.Status == SprintStatus.Cancelled)
                return false;

            if (sprint.Status == SprintStatus.Completed)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintAlreadyCompleted);
            }
                return true;

            if (sprint.Status == SprintStatus.Planned)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.SprintNotStarted);
            }
            // A previously scheduled job must do nothing if the end date was extended.
            if (sprint.EndDate > DateTime.UtcNow)
                return false;

            if (sprint.Status != SprintStatus.Active)
            {
                return Result.Failure<SprintStatusDto>(SprintErrors.InvalidSprintStatus);
            }

            sprint.Status = SprintStatus.Completed;
            sprint.EndDate = DateTime.UtcNow;

            foreach (var task in sprint.Tasks)
            {
                if (task.Status == TaskItemStatus.InProgress)
                {
                    task.Status = TaskItemStatus.ToDo;
                }
            }

            _logger.LogInformation("Sprint {SprintId} completed for Project {ProjectId}", sprintId, projectId);

            return Result.Success(new SprintStatusDto
            {
                SprintId = sprint.Id,
                Status = sprint.Status.ToString()
            });
        }

        public async Task<Result<ActiveSprintDto>> GetActiveSprintAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty) return Result.Failure<ActiveSprintDto>(SprintErrors.InvalidProject);

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<ActiveSprintDto>(SprintErrors.ProjectNotFound);
            }

            var activeSprint = await _sprintRepository.GetActiveSprintByProjectIdAsync(projectId, cancellationToken);
            if (activeSprint == null)
            {
                return Result.Failure<ActiveSprintDto>(SprintErrors.SprintNotFound);
            }

            var sprintWithTasks = await _sprintRepository.GetSprintWithTasksAsync(activeSprint.Id, cancellationToken);
            if (sprintWithTasks == null)
            {
                return Result.Failure<ActiveSprintDto>(SprintErrors.SprintNotFound);
            }

            int daysRemaining = (activeSprint.EndDate.Date - DateTime.UtcNow.Date).Days;
            if (daysRemaining < 0) daysRemaining = 0;

            double completionPercentage = 0;
            var totalTasks = sprintWithTasks.Tasks.Count;
            if (totalTasks > 0)
            {
                var doneTasks = sprintWithTasks.Tasks.Count(t => t.Status == TaskItemStatus.Done);
                completionPercentage = Math.Round((double)doneTasks / totalTasks * 100, 2);
            }
            sprintRepository.Update(sprint);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new ActiveSprintDto
            {
                SprintId = activeSprint.Id,
                TitleEn = activeSprint.TitleEn,
                TitleAr = activeSprint.TitleAr,
                StartDate = activeSprint.StartDate,
                EndDate = activeSprint.EndDate,
                DaysRemaining = daysRemaining,
                CompletionPercentage = completionPercentage
            });
            return true;
        }
    }
}