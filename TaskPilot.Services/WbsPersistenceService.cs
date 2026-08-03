using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.DTOs;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Services.Helpers;

namespace TaskPilot.Services
{
    public class WbsPersistenceService : IWbsPersistenceService
    {
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly ITaskRepository _taskRepository;

        public WbsPersistenceService(
            IUserStoryRepository userStoryRepository,
            ITaskRepository taskRepository)
        {
            _userStoryRepository = userStoryRepository;
            _taskRepository = taskRepository;
        }

        public async Task<Result<WbsPersistenceResult>> PersistAsync(
            Guid projectId,
            GeneratedWbs wbs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userStories = new List<UserStory>();
                var tasks = new List<TaskItem>();
                
                var storyDict = new Dictionary<string, UserStory>();

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

                    if (!string.IsNullOrWhiteSpace(generatedStory.Id))
                    {
                        storyDict[generatedStory.Id] = story;
                    }

                    userStories.Add(story);

                    foreach (var generatedTask in generatedStory.Tasks)
                    {
                        var taskItem = new TaskItem
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
                        };
                        tasks.Add(taskItem);
                    }
                }

                // Second pass to resolve dependencies
                foreach (var generatedStory in wbs.UserStories)
                {
                    if (!string.IsNullOrWhiteSpace(generatedStory.Id) &&
                        !string.IsNullOrWhiteSpace(generatedStory.DependsOnStoryId) &&
                        storyDict.TryGetValue(generatedStory.Id, out var story) &&
                        storyDict.TryGetValue(generatedStory.DependsOnStoryId, out var dependsOnStory))
                    {
                        story.DependsOnStory = dependsOnStory;
                    }
                }

                await _userStoryRepository.AddRangeAsync(userStories);
                await _taskRepository.AddRangeAsync(tasks);

                var wbsResult = new WbsPersistenceResult
                {
                    ProjectId = projectId,
                    UserStoriesCreated = userStories.Count,
                    TasksCreated = tasks.Count
                };
                return Result.Success(wbsResult);
            }
            catch (Exception ex)
            {
                return Result.Failure<WbsPersistenceResult>(CommonErrors.OperationFailed(ex.Message));
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
