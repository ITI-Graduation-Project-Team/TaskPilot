using System;
using System.Linq;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class BacklogService : IBacklogService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<UserStory> _userStoryRepository;
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public BacklogService(
            IRepository<Project> projectRepository,
            IRepository<UserStory> userStoryRepository,
            IRepository<TaskItem> taskRepository,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<BacklogDto>> GetBacklogAsync(Guid projectId)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result<BacklogDto>.Failure(new Error("Project.NotFound",ErrorType.NotFound, "Project not found."));
            }

            // Fetch only unassigned stories from the database
            var unassignedStories = (await _userStoryRepository.FindAsync(s => s.ProjectId == projectId && s.SprintId == null)).ToList();
            
            var storyIds = unassignedStories.Select(s => s.Id).ToList();
            var tasks = await _taskRepository.FindAsync(t => t.UserStoryId.HasValue && storyIds.Contains(t.UserStoryId.Value));

            var dto = new BacklogDto
            {
                ProjectId = project.Id,
                ProjectNameEn = project.NameEn,
                ProjectNameAr = project.NameAr,
                UserStories = unassignedStories.Select(s => new UserStoryDto
                {
                    Id = s.Id,
                    ProjectId = s.ProjectId,
                    TitleEn = s.TitleEn,
                    TitleAr = s.TitleAr,
                    DescriptionEn = s.DescriptionEn,
                    DescriptionAr = s.DescriptionAr,
                    AcceptanceCriteriaEn = s.AcceptanceCriteriaEn,
                    AcceptanceCriteriaAr = s.AcceptanceCriteriaAr,
                    Priority = s.Priority.ToString(),
                    Status = s.Status.ToString(),
                    Tasks = tasks.Where(t => t.UserStoryId == s.Id).Select(t => new TaskItemDto
                    {
                        Id = t.Id,
                        UserStoryId = t.UserStoryId.Value,
                        TitleEn = t.TitleEn,
                        TitleAr = t.TitleAr,
                        DescriptionEn = t.DescriptionEn,
                        DescriptionAr = t.DescriptionAr,
                        TechnicalSummaryEn = t.TechnicalSummaryEn,
                        TechnicalSummaryAr = t.TechnicalSummaryAr,
                        AcceptanceCriteriaEn = t.AcceptanceCriteriaEn,
                        AcceptanceCriteriaAr = t.AcceptanceCriteriaAr,
                        EstimatedHours = t.EstimatedHours,
                        EffortSize = t.EffortSize.ToString(),
                        Type = t.Type.ToString(),
                        Priority = t.Priority.ToString(),
                        Status = t.Status.ToString()
                    }).ToList()
                }).ToList()
            };

            return Result<BacklogDto>.Success(dto);
        }

        public async Task<Result<UserStoryDto>> CreateUserStoryAsync(Guid projectId, CreateUserStoryDto request)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return Result<UserStoryDto>.Failure(new Error("Project.NotFound", ErrorType.NotFound, "Project not found."));

            var story = new UserStory
            {
                ProjectId = projectId,
                TitleEn = request.TitleEn,
                TitleAr = request.TitleAr ?? string.Empty,
                DescriptionEn = request.DescriptionEn,
                DescriptionAr = request.DescriptionAr,
                AcceptanceCriteriaEn = request.AcceptanceCriteriaEn,
                AcceptanceCriteriaAr = request.AcceptanceCriteriaAr,
                Priority = request.Priority,
                Status = StoryStatus.ToDo,
                SprintId = null
            };

            await _userStoryRepository.AddAsync(story);
            await _unitOfWork.SaveChangesAsync();

            var dto = new UserStoryDto
            {
                Id = story.Id,
                ProjectId = story.ProjectId,
                TitleEn = story.TitleEn,
                TitleAr = story.TitleAr,
                DescriptionEn = story.DescriptionEn,
                DescriptionAr = story.DescriptionAr,
                AcceptanceCriteriaEn = story.AcceptanceCriteriaEn,
                AcceptanceCriteriaAr = story.AcceptanceCriteriaAr,
                Priority = story.Priority.ToString(),
                Status = story.Status.ToString()
            };

            return Result<UserStoryDto>.Success(dto);
        }

        public async Task<Result> UpdateUserStoryAsync(Guid storyId, UpdateUserStoryDto request)
        {
            var story = await _userStoryRepository.GetByIdAsync(storyId);
            if (story == null)
                return Result.Failure(new Error("UserStory.NotFound", ErrorType.NotFound, "User story not found."));

            story.TitleEn = request.TitleEn;
            story.TitleAr = request.TitleAr ?? string.Empty;
            story.DescriptionEn = request.DescriptionEn;
            story.DescriptionAr = request.DescriptionAr;
            story.AcceptanceCriteriaEn = request.AcceptanceCriteriaEn;
            story.AcceptanceCriteriaAr = request.AcceptanceCriteriaAr;
            story.Priority = request.Priority;

            _userStoryRepository.Update(story);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> DeleteUserStoryAsync(Guid storyId)
        {
            var story = await _userStoryRepository.GetByIdAsync(storyId);
            if (story == null)
                return Result.Failure(new Error("UserStory.NotFound", ErrorType.NotFound, "User story not found."));

            var tasks = await _taskRepository.FindAsync(t => t.UserStoryId == storyId);
            foreach (var task in tasks)
            {
                _taskRepository.Delete(task);
            }

            _userStoryRepository.Delete(story);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result<TaskItemDto>> CreateTaskAsync(Guid storyId, CreateTaskDto request)
        {
            var story = await _userStoryRepository.GetByIdAsync(storyId);
            if (story == null)
                return Result<TaskItemDto>.Failure(new Error("UserStory.NotFound", ErrorType.NotFound, "User story not found."));

            var task = new TaskItem
            {
                UserStoryId = storyId,
                TitleEn = request.TitleEn,
                TitleAr = request.TitleAr ?? string.Empty,
                DescriptionEn = request.DescriptionEn,
                DescriptionAr = request.DescriptionAr,
                TechnicalSummaryEn = request.TechnicalSummaryEn,
                TechnicalSummaryAr = request.TechnicalSummaryAr,
                AcceptanceCriteriaEn = request.AcceptanceCriteriaEn,
                AcceptanceCriteriaAr = request.AcceptanceCriteriaAr,
                EstimatedHours = request.EstimatedHours,
                EffortSize = request.EffortSize,
                Type = request.Type,
                Priority = request.Priority,
                Status = TaskItemStatus.ToDo,
                SprintId = null
            };

            await _taskRepository.AddAsync(task);
            await _unitOfWork.SaveChangesAsync();

            var dto = new TaskItemDto
            {
                Id = task.Id,
                UserStoryId = task.UserStoryId.Value,
                TitleEn = task.TitleEn,
                TitleAr = task.TitleAr,
                DescriptionEn = task.DescriptionEn,
                DescriptionAr = task.DescriptionAr,
                TechnicalSummaryEn = task.TechnicalSummaryEn,
                TechnicalSummaryAr = task.TechnicalSummaryAr,
                AcceptanceCriteriaEn = task.AcceptanceCriteriaEn,
                AcceptanceCriteriaAr = task.AcceptanceCriteriaAr,
                EstimatedHours = task.EstimatedHours,
                EffortSize = task.EffortSize.ToString(),
                Type = task.Type.ToString(),
                Priority = task.Priority.ToString(),
                Status = task.Status.ToString()
            };

            return Result<TaskItemDto>.Success(dto);
        }

        public async Task<Result> UpdateTaskAsync(Guid taskId, UpdateTaskDto request)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                return Result.Failure(new Error("TaskItem.NotFound", ErrorType.NotFound, "Task not found."));

            task.TitleEn = request.TitleEn;
            task.TitleAr = request.TitleAr ?? string.Empty;
            task.DescriptionEn = request.DescriptionEn;
            task.DescriptionAr = request.DescriptionAr;
            task.TechnicalSummaryEn = request.TechnicalSummaryEn;
            task.TechnicalSummaryAr = request.TechnicalSummaryAr;
            task.AcceptanceCriteriaEn = request.AcceptanceCriteriaEn;
            task.AcceptanceCriteriaAr = request.AcceptanceCriteriaAr;
            task.Priority = request.Priority;
            task.EstimatedHours = request.EstimatedHours;
            task.EffortSize = request.EffortSize;
            task.Type = request.Type;
            task.Status = request.Status;

            _taskRepository.Update(task);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> DeleteTaskAsync(Guid taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                return Result.Failure(new Error("TaskItem.NotFound", ErrorType.NotFound, "Task not found."));

            _taskRepository.Delete(task);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
