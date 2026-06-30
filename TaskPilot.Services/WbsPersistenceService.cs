using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.DTOs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class WbsPersistenceService : IWbsPersistenceService
    {
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public WbsPersistenceService(
            IUserStoryRepository userStoryRepository,
            ITaskRepository taskRepository,
            IUnitOfWork unitOfWork)
        {
            _userStoryRepository = userStoryRepository;
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<WbsPersistenceResult> PersistAsync(
            Guid projectId,
            GeneratedWbs wbs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userStories = new List<UserStory>();
                var tasks = new List<TaskItem>();

                foreach (var generatedStory in wbs.UserStories)
                {
                    var story = new UserStory
                    {
                        ProjectId = projectId,
                        SprintId = null,
                        TitleEn = generatedStory.TitleEn,
                        TitleAr = generatedStory.TitleAr,
                        DescriptionEn = generatedStory.DescriptionEn,
                        DescriptionAr = generatedStory.DescriptionAr,
                        AcceptanceCriteriaEn = generatedStory.AcceptanceCriteriaEn,
                        AcceptanceCriteriaAr = generatedStory.AcceptanceCriteriaAr,
                        Priority = MapPriority(generatedStory.Priority),
                        Status = StoryStatus.ToDo
                    };

                    userStories.Add(story);

                    foreach (var generatedTask in generatedStory.Tasks)
                    {
                        tasks.Add(new TaskItem
                        {
                            UserStory = story,
                            SprintId = null,
                            TitleEn = generatedTask.TitleEn,
                            TitleAr = generatedTask.TitleAr,
                            DescriptionEn = generatedTask.DescriptionEn,
                            DescriptionAr = generatedTask.DescriptionAr,
                            AcceptanceCriteriaEn = generatedTask.AcceptanceCriteriaEn,
                            AcceptanceCriteriaAr = generatedTask.AcceptanceCriteriaAr,
                            EffortSize = MapEffortSize(generatedTask.EffortSize),
                            Type = MapTaskType(generatedTask.Type),
                            Priority = MapTaskPriority(generatedTask.Priority),
                            EstimatedHours = generatedTask.EstimatedHours,
                            Status = TaskItemStatus.ToDo,
                            ActualHours = 0
                        });
                    }
                }

                await _userStoryRepository.AddRangeAsync(userStories, cancellationToken);
                await _taskRepository.AddRangeAsync(tasks, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return new WbsPersistenceResult
                {
                    ProjectId = projectId,
                    UserStoriesCreated = userStories.Count,
                    TasksCreated = tasks.Count,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                return new WbsPersistenceResult
                {
                    ProjectId = projectId,
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private static StoryPriority MapPriority(string value) =>
            value?.ToLower() switch
            {
                "high" => StoryPriority.High,
                "low" => StoryPriority.Low,
                _ => StoryPriority.Medium
            };

        private static EffortSize MapEffortSize(string value) =>
            value?.ToLower() switch
            {
                "small" => EffortSize.Small,
                "large" => EffortSize.Large,
                _ => EffortSize.Medium
            };

        private static TaskType MapTaskType(string value) =>
            value?.ToLower() switch
            {
                "nontechnical" => TaskType.NonTechnical,
                _ => TaskType.Technical
            };

        private static TaskPriority MapTaskPriority(string value) =>
            value?.ToLower() switch
            {
                "high" => TaskPriority.High,
                "low" => TaskPriority.Low,
                _ => TaskPriority.Medium
            };
    }
}
